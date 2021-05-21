// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Provides utility methods for common asynchronous operations.
    /// </summary>
    internal static class AsyncUtils
    {
        /// <summary>
        /// Creates a <see cref="SemaphoreSlim" /> with an handle initial and
        /// max count of one.
        /// </summary>
        /// <returns>A simple single handle <see cref="SemaphoreSlim" />.</returns>
        internal static SemaphoreSlim CreateSimpleLockingSemaphore()
        {
            return new SemaphoreSlim(initialCount: 1, maxCount: 1);
        }
    }
}
