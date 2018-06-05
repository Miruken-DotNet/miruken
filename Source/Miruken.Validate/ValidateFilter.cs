﻿namespace Miruken.Validate
{
    using System.Threading.Tasks;
    using Callback;
    using Callback.Policy;
    using Concurrency;

    public class ValidateFilter<TCb, TRes> : IFilter<TCb, TRes>
    {
        public int? Order { get; set; } = Stage.Validation;

        public Task<TRes> Next(TCb callback, MethodBinding method,
            IHandler composer, Next<TRes> next,
            IFilterProvider provider)
        {
            return Validate(callback, composer)
                .Then((req, s) => next())
                .Then((resp, s) => Validate(resp, composer));
        }

        private static Promise<T> Validate<T>(T target, IHandler handler)
        {
            return target == null ? Promise<T>.Empty
                : handler.ValidateAsync(target).Then((outcome, s) =>
                {
                    if (!outcome.IsValid)
                        throw new ValidationException(outcome);
                    return target;
                });
        }
    }
}
