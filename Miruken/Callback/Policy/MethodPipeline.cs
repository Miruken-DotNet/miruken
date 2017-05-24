﻿namespace Miruken.Callback.Policy
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    internal abstract class MethodPipeline
    {
        public abstract bool Invoke(MethodBinding binding, object target,
            object callback, Func<IHandler, object> complete, IHandler composer,
            IEnumerable<IFilter> filters, out object result);

        public static bool InvokeDynamic(MethodBinding binding, object target,
            object callback, Func<IHandler, object> complete, IHandler composer,
            IEnumerable<IDynamicFilter> filters, out object result)
        {
            var completed = false;
            using (var pipeline = filters.GetEnumerator())
            {
                NextDelegate<object> next = null;
                next = (proceed, comp) =>
                {
                    if (!proceed) return null;
                    while (pipeline.MoveNext())
                    {
                        composer = comp ?? composer;
                        var filter = pipeline.Current;
                        return filter?.Next(callback, binding, composer, next);
                    }
                    completed = true;
                    return complete(composer);
                };

                result = next(true, composer);
                return completed;
            }
        }

        public static MethodPipeline GetPipeline(Type callbackType, Type resultType)
        {
            if (resultType == typeof(void))
                resultType = typeof(object);
            var key = Tuple.Create(callbackType, resultType);
            return _pipelines.GetOrAdd(key, k =>
                (MethodPipeline)Activator.CreateInstance(
                    typeof(MethodPipeline<,>).MakeGenericType(k.Item1, k.Item2)
            ));
        }

        private static readonly ConcurrentDictionary<Tuple<Type, Type>, MethodPipeline>
            _pipelines = new ConcurrentDictionary<Tuple<Type, Type>, MethodPipeline>();
    }

    internal class MethodPipeline<Cb, Res> : MethodPipeline
    {
        public override bool Invoke(MethodBinding binding, object target, 
            object callback, Func<IHandler, object> complete, IHandler composer,
            IEnumerable<IFilter> filters, out object result)
        {
            var completed = false;
            using (var pipeline = filters.GetEnumerator())
            {
                NextDelegate<Res> next = null;
                next = (proceed, comp) =>
                {
                    if (!proceed) return default(Res);
                    while (pipeline.MoveNext())
                    {
                        composer = comp ?? composer;
                        var filter      = pipeline.Current;
                        var typedFilter = filter as IFilter<Cb, Res>;
                        if (typedFilter != null)
                            return typedFilter.Next((Cb)callback, binding, composer, next);
                        var dynamicFilter = filter as IDynamicFilter;
                        return (Res)dynamicFilter?.Next(
                            callback, binding, composer, (p,c) => next(p,c));
                    }
                    completed = true;
                    return (Res)complete(composer);
                };

                result = next(true, composer);
                return completed;
            }
        }
    }
}
