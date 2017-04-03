﻿namespace Miruken.Callback.Policy
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using Infrastructure;

    #region Method Binding

    public enum MethodBinding
    {
        LateBound,
        OpenGeneric,
        FastNoArgsVoid,
        FastOneArgVoid,
        FastTwoArgsVoid,
        FastThreeArgsVoid,
        FastNoArgsReturn,
        FastOneArgReturn,
        FastTwoArgsReturn,
        FastThreeArgsReturn
    }

    #endregion

    public abstract class MethodDefinition : IComparable<MethodDefinition>
    {
        private Delegate _delegate;
        private MethodBinding _binding;
        private Tuple<int, int>[] _mapping;
        private readonly List<ICallbackFilter> _filters;

        protected MethodDefinition(MethodInfo method)
        {
            Method     = method;
            ReturnType = method.ReturnType;
            IsVoid     = ReturnType == typeof(void);
            _filters   = new List<ICallbackFilter>();
        }

        public    MethodInfo            Method       { get; }
        public    Type                  ReturnType   { get; }
        public    bool                  IsVoid       { get; }
        public    Type                  VarianceType { get; set; }
        public    bool                  Untyped => 
            VarianceType == null || VarianceType == typeof(object);

        public bool Accepts(object callback, IHandler composer)
        {
            return _filters.All(f => f.Accepts(callback, composer));
        }

        public abstract bool Dispatch(object target, object callback, IHandler composer);

        internal void AddFilters(params ICallbackFilter[] filters)
        {
            _filters.AddRange(filters);
        }

        public abstract int CompareTo(MethodDefinition other);

        protected object Invoke(object target, object[] args, Type returnType = null)
        {
            switch (_binding)
            {
                #region Fast Invocation
                case MethodBinding.FastNoArgsVoid:
                    AssertArgsCount(0, args);
                    ((NoArgsDelegate)_delegate)(target);
                    return null;
                case MethodBinding.FastOneArgVoid:
                    AssertArgsCount(1, args);
                    ((OneArgDelegate)_delegate)(target, args[0]);
                    return null;
                case MethodBinding.FastTwoArgsVoid:
                    AssertArgsCount(2, args);
                    ((TwoArgsDelegate)_delegate)(target, args[0], args[1]);
                    return null;
                case MethodBinding.FastThreeArgsVoid:
                    AssertArgsCount(3, args);
                    ((ThreeArgsDelegate)_delegate)(target, args[0], args[1], args[2]);
                    return null;
                case MethodBinding.FastNoArgsReturn:
                    AssertArgsCount(0, args);
                    return ((NoArgsReturnDelegate)_delegate)(target);
                case MethodBinding.FastOneArgReturn:
                    AssertArgsCount(1, args);
                    return ((OneArgReturnDelegate)_delegate)(target, args[0]);
                case MethodBinding.FastTwoArgsReturn:
                    AssertArgsCount(2, args);
                    return ((TwoArgsReturnDelegate)_delegate)(target, args[0], args[1]);
                case MethodBinding.FastThreeArgsReturn:
                    AssertArgsCount(3, args);
                    return ((ThreeArgsReturnDelegate)_delegate)(target, args[0], args[1], args[2]);
                #endregion
                default:
                    return InvokeLate(target, args, returnType);
            }
        }

        protected object InvokeLate(object target, object[] args, Type returnType = null)
        {
            var method = Method;
            var parameters = method.GetParameters();
            if (parameters.Length > (args?.Length ?? 0))
                throw new ArgumentException($"Method {GetDescription()} expects {parameters.Length} arguments");
            if (_mapping != null)
            {
                var argTypes = _mapping.Select(mapping =>
                {
                    if (mapping.Item1 < 0)  // return type
                    {
                        if (returnType == null)
                            throw new ArgumentException(
                                "Return type is unknown and cannot help infer types");
                        return returnType.GetGenericArguments()[mapping.Item2];
                    }
                    var arg = args?[mapping.Item1];
                    if (arg == null)
                        throw new ArgumentException($"Argument {mapping.Item1} is null and cannot help infer types");
                    return arg.GetType().GetGenericArguments()[mapping.Item2];
                }).ToArray();
                method = method.MakeGenericMethod(argTypes);
            }
            return method.Invoke(target, HandlerDescriptor.Binding, null, args,
                                 CultureInfo.InvariantCulture);
        }

        internal void Configure()
        {
            var parameters = Method.GetParameters();
            if (!Method.IsGenericMethodDefinition)
            {
                switch (parameters.Length)
                {
                    #region Early Bound
                    case 0:
                        if (IsVoid)
                        {
                            _delegate = RuntimeHelper.CreateActionNoArgs(Method);
                            _binding  = MethodBinding.FastNoArgsVoid;
                        }
                        else
                        {
                            _delegate = RuntimeHelper.CreateFuncNoArgs(Method);
                            _binding  = MethodBinding.FastNoArgsReturn;
                        }
                        return;
                    case 1:
                        if (IsVoid)
                        {
                            _delegate = RuntimeHelper.CreateActionOneArg(Method);
                            _binding  = MethodBinding.FastOneArgVoid;
                        }
                        else
                        {
                            _delegate = RuntimeHelper.CreateFuncOneArg(Method);
                            _binding  = MethodBinding.FastOneArgReturn;
                        }
                        return;
                    case 2:
                        if (IsVoid)
                        {
                            _delegate = RuntimeHelper.CreateActionTwoArgs(Method);
                            _binding  = MethodBinding.FastTwoArgsVoid;
                        }
                        else
                        {
                            _delegate = RuntimeHelper.CreateFuncTwoArgs(Method);
                            _binding  = MethodBinding.FastTwoArgsReturn;
                        }
                        return;
                    case 3:
                        if (IsVoid)
                        {
                            _delegate = RuntimeHelper.CreateActionThreeArgs(Method);
                            _binding  = MethodBinding.FastThreeArgsVoid;
                        }
                        else
                        {
                            _delegate = RuntimeHelper.CreateFuncThreeArgs(Method);
                            _binding  = MethodBinding.FastThreeArgsReturn;
                        }
                        return;
                    #endregion
                    default:
                        _binding = MethodBinding.LateBound;
                        return;
                }
            }

            var argSources = parameters
                .Where(p => p.ParameterType.ContainsGenericParameters)
                .Select(p => Tuple.Create(p.Position, p.ParameterType))
                .ToList();
            var returnType = Method.ReturnType;
            if (returnType.ContainsGenericParameters)
                argSources.Add(Tuple.Create(-1, returnType));
            var methodArgs = Method.GetGenericArguments();
            var typeMapping = new Tuple<int, int>[methodArgs.Length];
            foreach (var source in argSources)
            {
                var typeArgs = source.Item2.GetGenericArguments();
                for (var i = 0; i < methodArgs.Length; ++i)
                {
                    if (typeMapping[i] != null) continue;
                    var index = Array.IndexOf(typeArgs, methodArgs[i]);
                    if (index >= 0)
                        typeMapping[i] = Tuple.Create(source.Item1, index);
                }
            }
            if (typeMapping.Contains(null))
                throw new InvalidOperationException(
                    $"Type mapping for {GetDescription()} could not be inferred");

            _mapping = typeMapping;
            _binding = MethodBinding.OpenGeneric;
        }

        protected string GetDescription()
        {
            return $"{Method.ReflectedType?.FullName}:{Method.Name}";
        }

        private static void AssertArgsCount(int expected, params object[] args)
        {
            if (args.Length != expected)
                throw new ArgumentException(
                    $"Expected {expected} arguments, but {args.Length} provided");
        }
    }

    public abstract class MethodDefinition<Attrib> : MethodDefinition
        where Attrib : DefinitionAttribute
    {
        protected MethodDefinition(MethodInfo method, 
                                   MethodRule<Attrib> rule, 
                                   Attrib attribute)
            : base(method)
        {
            Rule      = rule;
            Attribute = attribute;
        }

        public MethodRule<Attrib> Rule      { get; }
        public Attrib             Attribute { get; }

        public override bool Dispatch(object target, object callback, IHandler composer)
        {
            return Accepts(callback, composer) && Verify(target, callback, composer);
        }

        protected virtual bool Verify(object target, object callback, IHandler composer)
        {
            Invoke(target, callback, composer);
            return true;
        }

        protected virtual object Invoke(object target, object callback,
            IHandler composer, Type returnType = null)
        {
            var args = Rule.ResolveArgs(callback, composer);
            return Invoke(target, args, returnType);
        }
    }
}
