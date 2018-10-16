﻿using Miruken.Callback;
using Miruken.Concurrency;

namespace Miruken.Context
{
    public static class ContextExtensions
    {
        public static IHandler TrackPromise(
            this IHandler handler, Context context)
        {
            if (handler == null || context == null) return handler;
            return handler.Filter((callback, composer, proceed) =>
            {
                var handled = proceed();
                if (!handled) return false;
                var cb = callback as ICallback;
                if (!(cb?.Result is Promise promise)) return true;
                if (context.State == ContextState.Active)
                {
                    void Ended(Context ctx) => promise.Cancel();
                    context.ContextEnded += Ended;
                    promise.Finally(() => context.ContextEnded -= Ended);
                }
                else
                    promise.Cancel();
                return true;
            });
        }

        public static IHandler PublishFromRoot(this IHandler handler)
        {
            var context = handler.Resolve<Context>();
            return context != null ? context.Root.Publish() : handler;
        }
    }
}
