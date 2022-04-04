// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    internal class UnixConsoleOperations : IConsoleOperations
    {
        private const int LongWaitForKeySleepTime = 300;

        private const int ShortWaitForKeyTimeout = 5000;

        private const int ShortWaitForKeySpinUntilSleepTime = 30;

        private static readonly ManualResetEventSlim s_waitHandle = new();

        private static readonly SemaphoreSlim s_readKeyHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private static readonly SemaphoreSlim s_stdInHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private Func<CancellationToken, bool> WaitForKeyAvailable;

        /// <summary>
        /// Switch between long and short wait periods depending on if the user has recently (last 5
        /// seconds) pressed a key to avoid preventing the CPU from entering low power mode.
        /// </summary>
        internal UnixConsoleOperations() => WaitForKeyAvailable = LongWaitForKey;

        public ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken)
        {
            s_readKeyHandle.Wait(cancellationToken);

            // On Unix platforms System.Console.ReadKey has an internal lock on stdin.  Because
            // of this, if a ReadKey call is pending in one thread and in another thread
            // Console.CursorLeft is called, both threads block until a key is pressed.
            try
            {
                // The WaitForKeyAvailable delegate switches between a long delay between waits and
                // a short timeout depending on how recently a key has been pressed. This allows us
                // to let the CPU enter low power mode without compromising responsiveness.
                while (!WaitForKeyAvailable(cancellationToken)) { }
            }
            finally
            {
                s_readKeyHandle.Release();
            }

            // A key has been pressed, so acquire a lock on our internal stdin handle. This is done
            // so any of our calls to cursor position API's do not release ReadKey.
            s_stdInHandle.Wait(cancellationToken);
            try
            {
                return System.Console.ReadKey(intercept);
            }
            finally
            {
                s_stdInHandle.Release();
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

        private bool LongWaitForKey(CancellationToken cancellationToken)
        {
            // Wait for a key to be buffered (in other words, wait for Console.KeyAvailable to become
            // true) with a long delay between checks.
            while (!IsKeyAvailable(cancellationToken))
            {
                s_waitHandle.Wait(LongWaitForKeySleepTime, cancellationToken);
            }

            // As soon as a key is buffered, return true and switch the wait logic to be more
            // responsive, but also more expensive.
            WaitForKeyAvailable = ShortWaitForKey;
            return true;
        }

        private bool ShortWaitForKey(CancellationToken cancellationToken)
        {
            // Check frequently for a new key to be buffered.
            if (SpinUntilKeyAvailable(ShortWaitForKeyTimeout, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            // If the user has not pressed a key before the end of the SpinUntil timeout then
            // the user is idle and we can switch back to long delays between KeyAvailable checks.
            cancellationToken.ThrowIfCancellationRequested();
            WaitForKeyAvailable = LongWaitForKey;
            return false;
        }

        private static bool SpinUntilKeyAvailable(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return SpinWait.SpinUntil(
                () =>
                {
                    s_waitHandle.Wait(ShortWaitForKeySpinUntilSleepTime, cancellationToken);
                    return IsKeyAvailable(cancellationToken);
                },
                millisecondsTimeout);
        }

        private static bool IsKeyAvailable(CancellationToken cancellationToken)
        {
            s_stdInHandle.Wait(cancellationToken);
            try
            {
                return System.Console.KeyAvailable;
            }
            finally
            {
                s_stdInHandle.Release();
            }
        }
    }
}
