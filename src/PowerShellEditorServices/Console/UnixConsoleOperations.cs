using System;
using System.Threading;
using System.Threading.Tasks;
using UnixConsoleEcho;

namespace Microsoft.PowerShell.EditorServices.Console
{
    internal class UnixConsoleOperations : IConsoleOperations
    {
        private const int LONG_READ_DELAY = 400;

        private const int SHORT_READ_TIMEOUT = 5000;

        private SemaphoreSlim _readKeyHandle = new SemaphoreSlim(1, 1);

        internal UnixConsoleOperations()
        {
            // Switch between long and short wait periods depending on if the
            // user has recently (last 5 seconds) pressed a key to avoid preventing
            // the CPU from entering low power mode.
            WaitForKeyAvailable = LongWaitForKey;
        }

        public async Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken)
        {
            await _readKeyHandle.WaitAsync(cancellationToken);

            // I tried to replace this library with a call to `stty -echo`, but unfortunately
            // the library also sets up allowing backspace to trigger `Console.KeyAvailable`.
            InputEcho.Disable();
            try
            {
                while (!await WaitForKeyAvailable(cancellationToken));
            }
            finally
            {
                InputEcho.Enable();
                _readKeyHandle.Release();
            }

            return System.Console.ReadKey(intercept: true);
        }

        private Func<CancellationToken, Task<bool>> WaitForKeyAvailable;

        private async Task<bool> LongWaitForKey(CancellationToken cancellationToken)
        {
            while (!System.Console.KeyAvailable)
            {
                await Task.Delay(LONG_READ_DELAY, cancellationToken);
            }

            WaitForKeyAvailable = ShortWaitForKey;
            return true;
        }

        private async Task<bool> ShortWaitForKey(CancellationToken cancellationToken)
        {
            if (await SpinUntilKeyAvailable(SHORT_READ_TIMEOUT))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();
            WaitForKeyAvailable = LongWaitForKey;
            return false;
        }

        private async Task<bool> SpinUntilKeyAvailable(int millisecondsTimeout)
        {
            return await Task<bool>.Factory.StartNew(
                () => SpinWait.SpinUntil(
                    () => System.Console.KeyAvailable,
                    millisecondsTimeout));
        }
    }
}