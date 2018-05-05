﻿namespace Miruken.Callback
{
    public class BoundedHandler : DecoratedHandler
    {
        private readonly object _bounds;

        public BoundedHandler(IHandler handler, object bounds = null)
            : base(handler)
        {
            _bounds = bounds;
        }

        protected override bool HandleCallback(
            object callback, ref bool greedy, IHandler composer)
        {
            var composition = callback as Composition;
            return (!((composition?.Callback ?? callback) is IBoundCallback bounded) 
                || bounded.Bounds != _bounds) &&
                base.HandleCallback(callback, ref greedy, composer);
        }
    }

    public static class HandlerBoundsExtensions
    {
        public static IHandler Break(this IHandler handler, object bounds = null)
        {
            return handler != null ? new BoundedHandler(handler, bounds) : null;
        }
    }
}
