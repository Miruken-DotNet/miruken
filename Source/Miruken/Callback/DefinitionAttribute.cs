namespace Miruken.Callback
{
    using System;
    using Policy;

    [AttributeUsage(AttributeTargets.Method,
        AllowMultiple = true, Inherited = false)]
    public abstract class DefinitionAttribute : Attribute
    {
        public object InKey  { get; protected set; }
        public object OutKey { get; protected set; }
        public bool   Strict { get; set; }

        public abstract CallbackPolicy CallbackPolicy { get; }

        public virtual bool Approve(object callback, PolicyMethodBinding binding)
        {
            return true;
        }

        protected const string _ = null;
    }
}