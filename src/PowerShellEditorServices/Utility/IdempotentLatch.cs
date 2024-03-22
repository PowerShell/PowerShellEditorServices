// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    internal class IdempotentLatch
    {
        private int _signaled;

        public IdempotentLatch() => _signaled = 0;

        public bool IsSignaled => _signaled != 0;

        public bool TryEnter() => Interlocked.Exchange(ref _signaled, 1) == 0;
    }
}
