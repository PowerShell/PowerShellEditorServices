// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal class UnixConsoleOperations : IConsoleOperations
    {
        private static readonly SemaphoreSlim s_readKeyHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private static readonly SemaphoreSlim s_stdInHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        public ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken)
        {
            // A key has been pressed, so acquire a lock on our internal stdin handle. This is done
            // so any of our calls to cursor position API's do not release ReadKey.
            s_stdInHandle.Wait(cancellationToken);
            s_readKeyHandle.Wait(cancellationToken);
            try
            {
                return System.Console.ReadKey(intercept);
            }
            finally
            {
                s_readKeyHandle.Release();
                s_stdInHandle.Release();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public int GetCursorLeft() => GetCursorLeft(CancellationToken.None);

        public int GetCursorLeft(CancellationToken cancellationToken)
        {
            s_stdInHandle.Wait(cancellationToken);
            try
            {
                return System.Console.CursorLeft;
            }
            finally
            {
                s_stdInHandle.Release();
            }
        }

        public int GetCursorTop() => GetCursorTop(CancellationToken.None);

        public int GetCursorTop(CancellationToken cancellationToken)
        {
            s_stdInHandle.Wait(cancellationToken);
            try
            {
                return System.Console.CursorTop;
            }
            finally
            {
                s_stdInHandle.Release();
            }
        }
    }
}
