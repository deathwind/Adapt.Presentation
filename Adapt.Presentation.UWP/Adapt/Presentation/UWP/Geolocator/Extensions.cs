﻿using System;
using System.Runtime.CompilerServices;
using Windows.Foundation;

namespace Adapt.Presentation.UWP.Geolocator
{
    internal static class Extensions
    {
        public static ConfiguredTaskAwaitable<T> AsTask<T>(this IAsyncOperation<T> self, bool continueOnCapturedContext)
        {
            return self.AsTask().ConfigureAwait(continueOnCapturedContext);
        }
    }
}
