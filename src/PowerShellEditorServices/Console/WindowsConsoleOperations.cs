using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    internal class WindowsConsoleOperations : IConsoleOperations
    {
        private ConsoleKeyInfo? _bufferedKey;

        private SemaphoreSlim _readKeyHandle = new SemaphoreSlim(1, 1);

        public int GetCursorLeft() => System.Console.CursorLeft;

        public int GetCursorLeft(CancellationToken cancellationToken) => System.Console.CursorLeft;

        public Task<int> GetCursorLeftAsync() => Task.FromResult(System.Console.CursorLeft);

        public Task<int> GetCursorLeftAsync(CancellationToken cancellationToken) => Task.FromResult(System.Console.CursorLeft);

        public int GetCursorTop() => System.Console.CursorTop;

        public int GetCursorTop(CancellationToken cancellationToken) => System.Console.CursorTop;

        public Task<int> GetCursorTopAsync() => Task.FromResult(System.Console.CursorTop);

        public Task<int> GetCursorTopAsync(CancellationToken cancellationToken) => Task.FromResult(System.Console.CursorTop);

        public async Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken)
        {
            await _readKeyHandle.WaitAsync(cancellationToken);
            try
            {
                return
                    _bufferedKey.HasValue
                        ? _bufferedKey.Value
                        : await Task.Factory.StartNew(
                            () => (_bufferedKey = System.Console.ReadKey(intercept: true)).Value);
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
