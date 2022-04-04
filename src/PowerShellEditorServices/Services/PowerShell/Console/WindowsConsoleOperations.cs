// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal class WindowsConsoleOperations : IConsoleOperations
    {
        private ConsoleKeyInfo? _bufferedKey;

        private readonly SemaphoreSlim _readKeyHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        public int GetCursorLeft() => System.Console.CursorLeft;

        public int GetCursorLeft(CancellationToken cancellationToken) => System.Console.CursorLeft;

        public int GetCursorTop() => System.Console.CursorTop;

        public int GetCursorTop(CancellationToken cancellationToken) => System.Console.CursorTop;

        public ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken)
        {
            _readKeyHandle.Wait(cancellationToken);
            try
            {
                return _bufferedKey ?? (_bufferedKey = System.Console.ReadKey(intercept)).Value;
            }
            finally
            {
                _readKeyHandle.Release();

                // Throw if we're cancelled so the buffered key isn't cleared.
                cancellationToken.ThrowIfCancellationRequested();
                _bufferedKey = null;
            }
        }
    }
}
