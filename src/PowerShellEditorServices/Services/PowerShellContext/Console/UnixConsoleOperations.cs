//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;
using UnixConsoleEcho;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    internal class UnixConsoleOperations : IConsoleOperations
    {
        private const int LongWaitForKeySleepTime = 300;

        private const int ShortWaitForKeyTimeout = 5000;

        private const int ShortWaitForKeySpinUntilSleepTime = 30;

        private static readonly ManualResetEventSlim s_waitHandle = new ManualResetEventSlim();

        private static readonly SemaphoreSlim s_readKeyHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private static readonly SemaphoreSlim s_stdInHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        private Func<CancellationToken, bool> WaitForKeyAvailable;

        private Func<CancellationToken, Task<bool>> WaitForKeyAvailableAsync;

        internal UnixConsoleOperations()
        {
            // Switch between long and short wait periods depending on if the
            // user has recently (last 5 seconds) pressed a key to avoid preventing
            // the CPU from entering low power mode.
            WaitForKeyAvailable = LongWaitForKey;
            WaitForKeyAvailableAsync = LongWaitForKeyAsync;
        }

        public ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken)
        {
            s_readKeyHandle.Wait(cancellationToken);

            // On Unix platforms System.Console.ReadKey has an internal lock on stdin.  Because
            // of this, if a ReadKey call is pending in one thread and in another thread
            // Console.CursorLeft is called, both threads block until a key is pressed.

            // To work around this we wait for a key to be pressed before actually calling Console.ReadKey.
            // However, any pressed keys during this time will be echoed to the console. To get around
            // this we use the UnixConsoleEcho package to disable echo prior to waiting.
            if (VersionUtils.IsPS6)
            {
                InputEcho.Disable();
            }

            try
            {
                // The WaitForKeyAvailable delegate switches between a long delay between waits and
                // a short timeout depending on how recently a key has been pressed. This allows us
                // to let the CPU enter low power mode without compromising responsiveness.
                while (!WaitForKeyAvailable(cancellationToken));
            }
            finally
            {
                if (VersionUtils.IsPS6)
                {
                    InputEcho.Disable();
                }
                s_readKeyHandle.Release();
            }

            // A key has been pressed, so aquire a lock on our internal stdin handle. This is done
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

        public async Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
        {
            await s_readKeyHandle.WaitAsync(cancellationToken).ConfigureAwait(false);

            // I tried to replace this library with a call to `stty -echo`, but unfortunately
            // the library also sets up allowing backspace to trigger `Console.KeyAvailable`.
            if (VersionUtils.IsPS6)
            {
                InputEcho.Disable();
            }

            try
            {
                while (!await WaitForKeyAvailableAsync(cancellationToken).ConfigureAwait(false)) ;
            }
            finally
            {
                if (VersionUtils.IsPS6)
                {
                    InputEcho.Enable();
                }
                s_readKeyHandle.Release();
            }

            await s_stdInHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return System.Console.ReadKey(intercept);
            }
            finally
            {
                s_stdInHandle.Release();
            }
        }

        public int GetCursorLeft()
        {
            return GetCursorLeft(CancellationToken.None);
        }

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

        public Task<int> GetCursorLeftAsync()
        {
            return GetCursorLeftAsync(CancellationToken.None);
        }

        public async Task<int> GetCursorLeftAsync(CancellationToken cancellationToken)
        {
            await s_stdInHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return System.Console.CursorLeft;
            }
            finally
            {
                s_stdInHandle.Release();
            }
        }

        public int GetCursorTop()
        {
            return GetCursorTop(CancellationToken.None);
        }

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

        public Task<int> GetCursorTopAsync()
        {
            return GetCursorTopAsync(CancellationToken.None);
        }

        public async Task<int> GetCursorTopAsync(CancellationToken cancellationToken)
        {
            await s_stdInHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
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

        private async Task<bool> LongWaitForKeyAsync(CancellationToken cancellationToken)
        {
            while (!await IsKeyAvailableAsync(cancellationToken).ConfigureAwait(false))
            {
                await Task.Delay(LongWaitForKeySleepTime, cancellationToken).ConfigureAwait(false);
            }

            WaitForKeyAvailableAsync = ShortWaitForKeyAsync;
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

        private async Task<bool> ShortWaitForKeyAsync(CancellationToken cancellationToken)
        {
            if (await SpinUntilKeyAvailableAsync(ShortWaitForKeyTimeout, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();
            WaitForKeyAvailableAsync = LongWaitForKeyAsync;
            return false;
        }

        private bool SpinUntilKeyAvailable(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return SpinWait.SpinUntil(
                () =>
                {
                    s_waitHandle.Wait(ShortWaitForKeySpinUntilSleepTime, cancellationToken);
                    return IsKeyAvailable(cancellationToken);
                },
                millisecondsTimeout);
        }

        private Task<bool> SpinUntilKeyAvailableAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return Task<bool>.Factory.StartNew(
                () => SpinWait.SpinUntil(
                    () =>
                    {
                        // The wait handle is never set, it's just used to enable cancelling the wait.
                        s_waitHandle.Wait(ShortWaitForKeySpinUntilSleepTime, cancellationToken);
                        return IsKeyAvailable(cancellationToken);
                    },
                    millisecondsTimeout));
        }

        private bool IsKeyAvailable(CancellationToken cancellationToken)
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

        private async Task<bool> IsKeyAvailableAsync(CancellationToken cancellationToken)
        {
            await s_stdInHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
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
