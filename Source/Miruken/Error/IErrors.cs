﻿using System;

namespace Miruken.Error
{
    using Concurrency;

    public interface IErrors
    {
        Promise HandleException(Exception exception, 
            object callback = null, object context = null);
    }
}
