﻿namespace Miruken
{
    using System;

    public interface IEither
    {
        bool   IsLeft { get; }
        object Value  { get; }
    }

    public class Either<TL, TR> : IEither
    {
        private readonly bool _isLeft;

        public Either(TL left)
        {
            Left    = left;
            _isLeft = true;
        }

        public Either(TR right)
        {
            Right   = right;
            _isLeft = false;
        }

        protected Either()
        {
        }

        public TL Left  { get; }
        public TR Right { get; }

        bool IEither.IsLeft => _isLeft;

        object IEither.Value => _isLeft ? Left : (object)Right;

        public void Match(Action<TL> matchLeft, Action<TR> matchRight)
        {
            if (matchLeft == null)
                throw new ArgumentNullException(nameof(matchLeft));

            if (matchRight == null)
                throw new ArgumentNullException(nameof(matchRight));

            if (_isLeft)
                matchLeft(Left);
            else
                matchRight(Right);
        }

        public T Match<T>(Func<TL, T> matchLeft, Func<TR, T> matchRight)
        {
            if (matchLeft == null)
                throw new ArgumentNullException(nameof(matchLeft));

            if (matchRight == null)
                throw new ArgumentNullException(nameof(matchRight));

            return _isLeft ? matchLeft(Left) : matchRight(Right);
        }

        public TL LeftOrDefault() => Match(l => l, r => default(TL));
        public TR RightOrDefault() => Match(l => default(TR), r => r);

        public static implicit operator Either<TL, TR>(TL left) => new Either<TL, TR>(left);
        public static implicit operator Either<TL, TR>(TR right) => new Either<TL, TR>(right);
    }

    public class Try<TE, TR> : Either<TE, TR>
    {
        public Try(TR result)
             : base(result)
        {
        }

        public Try(TE error)
            : base(error)
        {
            IsError = true;
        }

        protected Try()
        {
        }

        public bool IsError { get; }

        public static implicit operator Try<TE, TR>(TE error) => new Try<TE, TR>(error);
        public static implicit operator Try<TE, TR>(TR result) => new Try<TE, TR>(result);
    }
}
