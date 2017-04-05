﻿namespace Miruken.Callback
{
    using System;
    using System.Collections.Generic;

    public class Resolution : ICallback, ICallbackDispatch
    {
        private readonly List<object> _resolutions;
        private object _result;

        public Resolution(object key, bool many = false)
        {
            Key          = key;
            Many         = many;
            _resolutions = new List<object>();
        }

        public object Key  { get; }

        public bool   Many { get; }

        public ICollection<object> Resolutions => _resolutions.AsReadOnly();

        public Type ResultType => Key as Type;

        public object Result
        {
            get
            {
                if (_result != null) return _result;
                if (Many)
                    _result = _resolutions.ToArray();
                else if (_resolutions.Count > 0)
                    _result = _resolutions[0];

                return _result;
            }
            set { _result = value; }
        }

        public bool Resolve(object resolution, IHandler composer)
        {
            if (resolution == null ||
                (!Many && _resolutions.Count > 0)
                || _resolutions.Contains(resolution)
                || !IsSatisfied(resolution, composer))
                return false;

            _resolutions.Add(resolution);
            _result = null;
            return true;
        }

        protected virtual bool IsSatisfied(object resolution, IHandler composer)
        {
            return true;
        }

        bool ICallbackDispatch.Dispatch(Handler handler, bool greedy, IHandler composer)
        {
            var surrogate = handler.Surrogate;
            var handled   = surrogate != null && Implied(surrogate, false, composer);
            if (!handled || greedy)
                handled = Implied(handler, false, composer) || handled;
            if (handled && !greedy) return true;
            return HandlerMetadata.Dispatch(ProvidesAttribute.Policy,
                handler, this, greedy, composer) || handled;
        }

        private bool Implied(object item, bool invariant, IHandler composer)
        {
            var type = Key as Type;
            if (type == null) return false;

            var compatible = invariant
                           ? type == item.GetType()
                           : type.IsInstanceOfType(item);

            return compatible && Resolve(item, composer);
        }
    }
}
