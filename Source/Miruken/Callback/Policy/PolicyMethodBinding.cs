﻿namespace Miruken.Callback.Policy
{
    using System;
    using System.Linq;

    public delegate bool AcceptResultDelegate(
        object result, MethodBinding binding);

    public delegate PolicyMethodBinding BindMethodDelegate(
        CallbackPolicy policy,
        ref PolicyMethodBindingInfo policyMethodBindingInfo);

    public struct PolicyMethodBindingInfo
    {
        public PolicyMethodBindingInfo(
            MethodRule rule, MethodDispatch dispatch,
            DefinitionAttribute attribute)
        {
            Rule          = rule;
            Dispatch      = dispatch;
            Attribute     = attribute;
            Key           = attribute.Key;
            CallbackIndex = null;
        }

        public object              Key;
        public int?                CallbackIndex;
        public MethodRule          Rule           { get; }
        public MethodDispatch      Dispatch       { get; }
        public DefinitionAttribute Attribute      { get; }
    }

    public class PolicyMethodBinding : MethodBinding
    {
        public PolicyMethodBinding(CallbackPolicy policy,
                                   ref PolicyMethodBindingInfo bindingInfo)
            : base(bindingInfo.Dispatch)
        {
            Policy        = policy;
            Rule          = bindingInfo.Rule;
            Attribute     = bindingInfo.Attribute;
            CallbackIndex = bindingInfo.CallbackIndex;
            Key           = NormalizeKey(ref bindingInfo);
        }

        public MethodRule          Rule          { get; }
        public DefinitionAttribute Attribute     { get; }
        public CallbackPolicy      Policy        { get; }
        public int?                CallbackIndex { get; }
        public object              Key           { get; }

        public override bool Dispatch(object target, object callback, 
            IHandler composer, Func<object, bool> results = null)
        {
            if (Attribute?.Approve(callback, this) == false)
                return false;
            object result;
            var resultType = Policy.ResultType?.Invoke(callback);
            return Invoke(target, callback, composer, resultType,
                results, out result);
        }

        private bool Invoke(object target, object callback,
            IHandler composer, Type resultType, Func<object, bool> results,
            out object result)
        {
            var args = ResolveArgs(callback, composer);
            if (args == null)
            {
                result = null;
                return false;
            }

            if (callback is IInvokeOnlyCallback)
            {
                result = Dispatcher.Invoke(target, args, resultType);
                return true;
            }

            Type callbackType;
            var dispatcher     = Dispatcher.CloseDispatch(args, resultType);
            var actualCallback = GetCallbackInfo(
                callback, args, dispatcher, out callbackType);
            var returnType     = dispatcher.ReturnType;
            var logicalType    = dispatcher.LogicalReturnType;

            var filters = composer
                .GetOrderedFilters(this, callbackType, logicalType, Filters,
                    FilterAttribute.GetFilters(target.GetType(), true), 
                    Policy.Filters)
                .ToArray();

            if (filters.Length == 0)
                result = dispatcher.Invoke(target, args, resultType);
            else if (!MethodPipeline.GetPipeline(callbackType, logicalType)
                .Invoke(this, target, actualCallback, comp => dispatcher.Invoke(
                    target, UpdateArgs(args, composer, comp),
                        resultType), composer, filters, out result))
                return false;

            var accepted = Policy.AcceptResult?.Invoke(result, this)
                        ?? result != null;
            if (accepted && (result != null))
            {
                var asyncCallback = callback as IAsyncCallback;
                result = CoerceResult(result, returnType, asyncCallback?.WantsAsync);
                return results?.Invoke(result) != false;
            }

            return accepted;
        }

        private object[] ResolveArgs(object callback, IHandler composer)
        {
            var numRuleArgs = Rule.Args.Length;
            var arguments   = Dispatcher.Arguments;
            if (arguments.Length == numRuleArgs)
                return Rule.ResolveArgs(callback);

            var args = new object[arguments.Length];

            if (!composer.All(batch =>
            {
                for (var i = numRuleArgs; i < arguments.Length; ++i)
                {
                    var index        = i;
                    var argument     = arguments[i];
                    var argumentType = argument.ArgumentType;
                    var resolver     = argument.Resolver ?? DefaultResolver;
                    if (argumentType == typeof(IHandler))
                        args[i] = composer;
                    else if (argumentType.IsInstanceOfType(this))
                        args[i] = this;
                    else
                        batch.Add(h => args[index] =
                            resolver.ResolveArgument(argument, h, composer));
                }
            })) return null;

            var ruleArgs = Rule.ResolveArgs(callback);
            Array.Copy(ruleArgs, args, ruleArgs.Length);
            return args;
        }

        private object GetCallbackInfo(object callback, object[] args,
            MethodDispatch dispatcher, out Type callbackType)
        {
            if (CallbackIndex.HasValue)
            {
                var index = CallbackIndex.Value;
                callbackType = dispatcher.Arguments[index].ArgumentType;
                return args[index];
            }
            callbackType = callback.GetType();
            return callback;
        }

        private static object NormalizeKey(ref PolicyMethodBindingInfo bindingInfo)
        {
            var key = bindingInfo.Key;
            var varianceType = key as Type;
            if (varianceType == null) return key;
            if (varianceType.ContainsGenericParameters &&
                !varianceType.IsGenericTypeDefinition)
                varianceType = varianceType.GetGenericTypeDefinition();
            return varianceType;
        }

        private static object[] UpdateArgs(object[] args,
            IHandler oldComposer, IHandler newComposer)
        {
            return ReferenceEquals(oldComposer, newComposer) ? args
                 : args.Select(arg => ReferenceEquals(arg, oldComposer)
                 ? newComposer : arg).ToArray();
        }

        private static readonly ResolvingAttribute
            DefaultResolver = new ResolvingAttribute();
    }
}
