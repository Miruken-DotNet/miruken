﻿using System;

namespace Miruken.Callback
{
    [Flags]
    public enum CallbackOptions
    {
        None       = 0,
        Duck       = 1 << 0,
        Strict     = 1 << 1,
        Broadcast  = 1 << 2,
        BestEffort = 1 << 3,
        Notify     = Broadcast | BestEffort
    }

    public class CallbackSemantics 
        : Composition, IResolveCallback, IFilterCallback,
          IBatchCallback
    {
        private CallbackOptions _options;
        private CallbackOptions _specified;

        public static readonly CallbackSemantics None =
            new CallbackSemantics(CallbackOptions.None);

        public CallbackSemantics()
            : this(CallbackOptions.None)
        {    
        }

        public CallbackSemantics(CallbackOptions options)
        {
            _options = _specified = options;
        }

        public bool HasOption(CallbackOptions options)
        {
            return (_options & options) == options;
        }

        public void SetOption(CallbackOptions options, bool enabled)
        {
            _options = enabled
                     ? _options | options
                     : _options & ~options;
            _specified = _specified | options;
        }

        public bool IsSpecified(CallbackOptions options)
        {
            return (_specified & options) == options;
        }

        public void MergeInto(CallbackSemantics semantics)
        {
            MergeInto(semantics, CallbackOptions.Duck);
            MergeInto(semantics, CallbackOptions.Strict);
            MergeInto(semantics, CallbackOptions.BestEffort);
            MergeInto(semantics, CallbackOptions.Broadcast);
        }

        private void MergeInto(CallbackSemantics semantics, CallbackOptions option)
        {
            if (IsSpecified(option) && !semantics.IsSpecified(option))
                semantics.SetOption(option, HasOption(option));
        }

        bool IFilterCallback.AllowFiltering => false;
        bool IBatchCallback.AllowBatching => false;

        object IResolveCallback.GetResolveCallback()
        {
            return this;
        }
    }

    public class CallbackSemanticsDecorator : Handler, IDecorator
    {
        private readonly IHandler _handler;
        private readonly CallbackSemantics _semantics;

        public CallbackSemanticsDecorator(
            IHandler handler, CallbackOptions options)
        {
            _handler   = handler;
            _semantics = new CallbackSemantics(options);
        }

        object IDecorator.Decoratee => _handler;

        protected override bool HandleCallback(
            object callback, ref bool greedy, IHandler composer)
        {
            if (Composition.IsComposed<CallbackSemantics>(callback))
                return false;

            var semantics = callback as CallbackSemantics;
            if (semantics != null)
            {
                _semantics.MergeInto(semantics);
                if (greedy)
                    _handler.Handle(callback, ref greedy, composer);
                return true;
            }

            if (callback is Composition)
                return _handler.Handle(callback, ref greedy, composer);

            if (_semantics.HasOption(CallbackOptions.Broadcast))
                greedy = true;

            if (_semantics.HasOption(CallbackOptions.BestEffort))
            {
                try
                {
                    _handler.Handle(callback, ref greedy, composer);
                    return true;
                }
                catch (RejectedException)
                {
                    return true;
                }
            }

            return _handler.Handle(callback, ref greedy, composer);
        }
    }

    public static class CallbackSemanticExtensions
    {
        public static CallbackSemantics GetSemantics(this IHandler handler)
        {
            var semantics = new CallbackSemantics();
            return handler.Handle(semantics, true) ? semantics : null;           
        }

        public static IHandler Semantics(
            this IHandler handler, CallbackOptions options)
        {
            return handler == null ? null 
                 : new CallbackSemanticsDecorator(handler, options);
        }

        #region Semantics

        public static IHandler Duck(this IHandler handler)
        {
            return Semantics(handler, CallbackOptions.Duck);
        }

        public static IHandler Strict(this IHandler handler)
        {
            return Semantics(handler, CallbackOptions.Strict);
        }

        public static IHandler Broadcast(this IHandler handler)
        {
            return Semantics(handler, CallbackOptions.Broadcast);
        }

        public static IHandler BestEffort(this IHandler handler)
        {
            return Semantics(handler, CallbackOptions.BestEffort);
        }

        public static IHandler Notify(this IHandler handler)
        {
            return Semantics(handler, CallbackOptions.Notify);
        }

        #endregion
    }
}
