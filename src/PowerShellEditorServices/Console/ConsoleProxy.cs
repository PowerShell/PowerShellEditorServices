using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    internal static class ConsoleProxy
    {
        private static IConsoleOperations s_consoleProxy;

        static ConsoleProxy()
        {
            // Maybe we should just include the RuntimeInformation package for FullCLR?
            #if CoreCLR
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                s_consoleProxy = new WindowsConsoleOperations();
                return;
            }

            s_consoleProxy = new UnixConsoleOperations();
            #else
            s_consoleProxy = new WindowsConsoleOperations();
            #endif
        }

        public static Task<ConsoleKeyInfo> ReadKeyAsync(CancellationToken cancellationToken) =>
            s_consoleProxy.ReadKeyAsync(cancellationToken);

        public static int GetCursorLeft() =>
            s_consoleProxy.GetCursorLeft();

        public static int GetCursorLeft(CancellationToken cancellationToken) =>
            s_consoleProxy.GetCursorLeft(cancellationToken);

        public static Task<int> GetCursorLeftAsync() =>
            s_consoleProxy.GetCursorLeftAsync();

        public static Task<int> GetCursorLeftAsync(CancellationToken cancellationToken) =>
            s_consoleProxy.GetCursorLeftAsync(cancellationToken);

        public static int GetCursorTop() =>
            s_consoleProxy.GetCursorTop();

        public static int GetCursorTop(CancellationToken cancellationToken) =>
            s_consoleProxy.GetCursorTop(cancellationToken);

        public static Task<int> GetCursorTopAsync() =>
            s_consoleProxy.GetCursorTopAsync();

        public static Task<int> GetCursorTopAsync(CancellationToken cancellationToken) =>
            s_consoleProxy.GetCursorTopAsync(cancellationToken);

        /// <summary>
        /// On Unix platforms this method is sent to PSReadLine as a work around for issues
        /// with the System.Console implementation for that platform. Functionally it is the
        /// same as System.Console.ReadKey, with the exception that it will not lock the
        /// standard input stream.
        /// </summary>
        /// <param name="intercept">
        /// Determines whether to display the pressed key in the console window.
        /// true to not display the pressed key; otherwise, false.
        /// </param>
        /// <returns>
        /// An object that describes the ConsoleKey constant and Unicode character, if any,
        /// that correspond to the pressed console key. The ConsoleKeyInfo object also describes,
        /// in a bitwise combination of ConsoleModifiers values, whether one or more Shift, Alt,
        /// or Ctrl modifier keys was pressed simultaneously with the console key.
        /// </returns>
        internal static ConsoleKeyInfo UnixReadKey(bool intercept, CancellationToken cancellationToken)
        {
            try
            {
                return ((UnixConsoleOperations)s_consoleProxy).ReadKey(intercept, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return default(ConsoleKeyInfo);
            }
        }
    }
}
