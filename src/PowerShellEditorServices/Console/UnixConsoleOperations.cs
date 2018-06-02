using System;
using System.Threading;
using System.Threading.Tasks;
using UnixConsoleEcho;

namespace Microsoft.PowerShell.EditorServices.Console
{
    internal class UnixConsoleOperations : IConsoleOperations
    {
        private const int LONG_READ_DELAY = 300;

        private const int SHORT_READ_TIMEOUT = 5000;

        private static readonly ManualResetEventSlim s_waitHandle = new ManualResetEventSlim();

        private static readonly SemaphoreSlim s_readKeyHandle = new SemaphoreSlim(1, 1);

        private static readonly SemaphoreSlim s_stdInHandle = new SemaphoreSlim(1, 1);

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

        internal ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken)
        {
            s_readKeyHandle.Wait(cancellationToken);

            InputEcho.Disable();
            try
            {
                while (!WaitForKeyAvailable(cancellationToken));
            }
            finally
            {
                InputEcho.Disable();
                s_readKeyHandle.Release();
            }

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

        public async Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken)
        {
            await s_readKeyHandle.WaitAsync(cancellationToken);

            // I tried to replace this library with a call to `stty -echo`, but unfortunately
            // the library also sets up allowing backspace to trigger `Console.KeyAvailable`.
            InputEcho.Disable();
            try
            {
                while (!await WaitForKeyAvailableAsync(cancellationToken));
            }
            finally
            {
                InputEcho.Enable();
                s_readKeyHandle.Release();
            }

            await s_stdInHandle.WaitAsync(cancellationToken);
            try
            {
                return System.Console.ReadKey(intercept: true);
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

        public async Task<int> GetCursorLeftAsync()
        {
            return await GetCursorLeftAsync(CancellationToken.None);
        }

        public async Task<int> GetCursorLeftAsync(CancellationToken cancellationToken)
        {
            await s_stdInHandle.WaitAsync(cancellationToken);
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

        public async Task<int> GetCursorTopAsync()
        {
            return await GetCursorTopAsync(CancellationToken.None);
        }

        public async Task<int> GetCursorTopAsync(CancellationToken cancellationToken)
        {
            await s_stdInHandle.WaitAsync(cancellationToken);
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
            while (!IsKeyAvailable(cancellationToken))
            {
                s_waitHandle.Wait(LONG_READ_DELAY, cancellationToken);
            }

            WaitForKeyAvailable = ShortWaitForKey;
            return true;
        }

        private async Task<bool> LongWaitForKeyAsync(CancellationToken cancellationToken)
        {
            while (!await IsKeyAvailableAsync(cancellationToken))
            {
                await Task.Delay(LONG_READ_DELAY, cancellationToken);
            }

            WaitForKeyAvailableAsync = ShortWaitForKeyAsync;
            return true;
        }

        private bool ShortWaitForKey(CancellationToken cancellationToken)
        {
            if (SpinUntilKeyAvailable(SHORT_READ_TIMEOUT, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();
            WaitForKeyAvailable = LongWaitForKey;
            return false;
        }

        private async Task<bool> ShortWaitForKeyAsync(CancellationToken cancellationToken)
        {
            if (await SpinUntilKeyAvailableAsync(SHORT_READ_TIMEOUT, cancellationToken))
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
                    s_waitHandle.Wait(30, cancellationToken);
                    return IsKeyAvailable(cancellationToken);
                },
                millisecondsTimeout);
        }

        private async Task<bool> SpinUntilKeyAvailableAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return await Task<bool>.Factory.StartNew(
                () => SpinWait.SpinUntil(
                    () =>
                    {
                        // The wait handle is never set, it's just used to enable cancelling the wait.
                        s_waitHandle.Wait(30, cancellationToken);
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
            await s_stdInHandle.WaitAsync(cancellationToken);
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
