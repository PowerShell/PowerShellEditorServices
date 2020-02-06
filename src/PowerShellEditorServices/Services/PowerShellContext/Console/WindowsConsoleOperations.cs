//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal class WindowsConsoleOperations : IConsoleOperations
    {
        private ConsoleKeyInfo? _bufferedKey;

        private SemaphoreSlim _readKeyHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        public int GetCursorLeft() => System.Console.CursorLeft;

        public int GetCursorLeft(CancellationToken cancellationToken) => System.Console.CursorLeft;

        public Task<int> GetCursorLeftAsync() => Task.FromResult(System.Console.CursorLeft);

        public Task<int> GetCursorLeftAsync(CancellationToken cancellationToken) => Task.FromResult(System.Console.CursorLeft);

        public int GetCursorTop() => System.Console.CursorTop;

        public int GetCursorTop(CancellationToken cancellationToken) => System.Console.CursorTop;

        public Task<int> GetCursorTopAsync() => Task.FromResult(System.Console.CursorTop);

        public Task<int> GetCursorTopAsync(CancellationToken cancellationToken) => Task.FromResult(System.Console.CursorTop);

        public async Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
        {
            await _readKeyHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_bufferedKey == null)
                {
                    _bufferedKey = await Task.Run(() => Console.ReadKey(intercept)).ConfigureAwait(false);
                }

                return _bufferedKey.Value;
            }
            finally
            {
                _readKeyHandle.Release();

                // Throw if we're cancelled so the buffered key isn't cleared.
                cancellationToken.ThrowIfCancellationRequested();
                _bufferedKey = null;
            }
        }

        public ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken)
        {
            _readKeyHandle.Wait(cancellationToken);
            try
            {
                return
                    _bufferedKey.HasValue
                        ? _bufferedKey.Value
                        : (_bufferedKey = System.Console.ReadKey(intercept)).Value;
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
