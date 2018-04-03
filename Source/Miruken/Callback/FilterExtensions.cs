﻿namespace Miruken.Callback
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Policy;

    public static class FilterExtensions
    {
        public static IHandler WithFilters(
            this IHandler handler, params IFilter[] filters)
        {
            return handler == null ? null :
                new FilterOptions
                {
                    ExtraFilters = new []
                    {
                        new FilterInstancesProvider(filters)
                    }
                }
                .Decorate(handler);
        }

        public static IHandler WithFilters(
             this IHandler handler, params IFilterProvider[] providers)
        {
            return handler == null ? null :
                new FilterOptions { ExtraFilters = providers }
                .Decorate(handler);
        }

        public static IEnumerable<IFilter> GetOrderedFilters(
            this IHandler composer, MethodBinding binding, Type callbackType, 
            Type logicalResultType, FilterOptions options, params 
            IEnumerable<IFilterProvider>[] providers)
        {
            if (logicalResultType == typeof(void))
                logicalResultType = typeof(object);

            return new SortedSet<IFilter>(providers
                .Where(p => p != null)
                .SelectMany(p => p)
                .Concat(options?.ExtraFilters ??
                        Enumerable.Empty<IFilterProvider>())
                .Where(provider => provider != null)
                .SelectMany(provider => provider
                    .GetFilters(binding, callbackType, logicalResultType, composer)
                    .Where(filter => filter != null)),
                    FilterComparer.Instance);
        }

        public static IEnumerable<IFilter> GetOrderedFilters(
            this IHandler handler, MethodBinding binding, Type callbackType, 
            Type logicalResultType, params IEnumerable<IFilterProvider>[] providers)
        {
            var options = handler.GetOptions<FilterOptions>();
            return handler.GetOrderedFilters(binding, callbackType,
                logicalResultType, options, providers);
        }
    }
}
