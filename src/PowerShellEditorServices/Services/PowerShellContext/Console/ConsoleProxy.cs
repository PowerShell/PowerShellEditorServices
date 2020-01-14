//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides asynchronous implementations of the <see cref="Console" /> API's as well as
    /// synchronous implementations that work around platform specific issues.
    /// </summary>
    internal static class ConsoleProxy
    {
        private static IConsoleOperations s_consoleProxy;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline", Justification = "Platform specific initialization")]
        static ConsoleProxy()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                s_consoleProxy = new WindowsConsoleOperations();
                return;
            }

            s_consoleProxy = new UnixConsoleOperations();
        }

        /// <summary>
        /// Obtains the next character or function key pressed by the user asynchronously.
        /// Does not block when other console API's are called.
        /// </summary>
        /// <param name="intercept">
        /// Determines whether to display the pressed key in the console window. <see langword="true" />
        /// to not display the pressed key; otherwise, <see langword="false" />.
        /// </param>
        /// <param name="cancellationToken">The CancellationToken to observe.</param>
        /// <returns>
        /// An object that describes the <see cref="ConsoleKey" /> constant and Unicode character, if any,
        /// that correspond to the pressed console key. The <see cref="ConsoleKeyInfo" /> object also
        /// describes, in a bitwise combination of <see cref="ConsoleModifiers" /> values, whether
        /// one or more Shift, Alt, or Ctrl modifier keys was pressed simultaneously with the console key.
        /// </returns>
        public static ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken) =>
            s_consoleProxy.ReadKey(intercept, cancellationToken);

        /// <summary>
        /// Obtains the next character or function key pressed by the user asynchronously.
        /// Does not block when other console API's are called.
        /// </summary>
        /// <param name="intercept">
        /// Determines whether to display the pressed key in the console window. <see langword="true" />
        /// to not display the pressed key; otherwise, <see langword="false" />.
        /// </param>
        /// <param name="cancellationToken">The CancellationToken to observe.</param>
        /// <returns>
        /// A task that will complete with a result of the key pressed by the user.
        /// </returns>
        public static Task<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken) =>
            s_consoleProxy.ReadKeyAsync(intercept, cancellationToken);

        /// <summary>
        /// Obtains the horizontal position of the console cursor. Use this method
        /// instead of <see cref="System.Console.CursorLeft" /> to avoid triggering
        /// pending calls to <see cref="IConsoleOperations.ReadKeyAsync(bool, CancellationToken)" />
        /// on Unix platforms.
        /// </summary>
        /// <returns>The horizontal position of the console cursor.</returns>
        public static int GetCursorLeft() =>
            s_consoleProxy.GetCursorLeft();

        /// <summary>
        /// Obtains the horizontal position of the console cursor. Use this method
        /// instead of <see cref="System.Console.CursorLeft" /> to avoid triggering
        /// pending calls to <see cref="IConsoleOperations.ReadKeyAsync(bool, CancellationToken)" />
        /// on Unix platforms.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> to observe.</param>
        /// <returns>The horizontal position of the console cursor.</returns>
        public static int GetCursorLeft(CancellationToken cancellationToken) =>
            s_consoleProxy.GetCursorLeft(cancellationToken);

        /// <summary>
        /// Obtains the horizontal position of the console cursor. Use this method
        /// instead of <see cref="System.Console.CursorLeft" /> to avoid triggering
        /// pending calls to <see cref="IConsoleOperations.ReadKeyAsync(bool, CancellationToken)" />
        /// on Unix platforms.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{T}" /> representing the asynchronous operation. The
        /// <see cref="Task{T}.Result" /> property will return the horizontal position
        /// of the console cursor.
        /// </returns>
        public static Task<int> GetCursorLeftAsync() =>
            s_consoleProxy.GetCursorLeftAsync();

        /// <summary>
        /// Obtains the horizontal position of the console cursor. Use this method
        /// instead of <see cref="System.Console.CursorLeft" /> to avoid triggering
        /// pending calls to <see cref="IConsoleOperations.ReadKeyAsync(bool, CancellationToken)" />
        /// on Unix platforms.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> to observe.</param>
        /// <returns>
        /// A <see cref="Task{T}" /> representing the asynchronous operation. The
        /// <see cref="Task{T}.Result" /> property will return the horizontal position
        /// of the console cursor.
        /// </returns>
        public static Task<int> GetCursorLeftAsync(CancellationToken cancellationToken) =>
            s_consoleProxy.GetCursorLeftAsync(cancellationToken);

        /// <summary>
        /// Obtains the vertical position of the console cursor. Use this method
        /// instead of <see cref="System.Console.CursorTop" /> to avoid triggering
        /// pending calls to <see cref="IConsoleOperations.ReadKeyAsync(bool, CancellationToken)" />
        /// on Unix platforms.
        /// </summary>
        /// <returns>The vertical position of the console cursor.</returns>
        public static int GetCursorTop() =>
            s_consoleProxy.GetCursorTop();

        /// <summary>
        /// Obtains the vertical position of the console cursor. Use this method
        /// instead of <see cref="System.Console.CursorTop" /> to avoid triggering
        /// pending calls to <see cref="IConsoleOperations.ReadKeyAsync(bool, CancellationToken)" />
        /// on Unix platforms.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> to observe.</param>
        /// <returns>The vertical position of the console cursor.</returns>
        public static int GetCursorTop(CancellationToken cancellationToken) =>
            s_consoleProxy.GetCursorTop(cancellationToken);

        /// <summary>
        /// Obtains the vertical position of the console cursor. Use this method
        /// instead of <see cref="System.Console.CursorTop" /> to avoid triggering
        /// pending calls to <see cref="IConsoleOperations.ReadKeyAsync(bool, CancellationToken)" />
        /// on Unix platforms.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{T}" /> representing the asynchronous operation. The
        /// <see cref="Task{T}.Result" /> property will return the vertical position
        /// of the console cursor.
        /// </returns>
        public static Task<int> GetCursorTopAsync() =>
            s_consoleProxy.GetCursorTopAsync();

        /// <summary>
        /// Obtains the vertical position of the console cursor. Use this method
        /// instead of <see cref="System.Console.CursorTop" /> to avoid triggering
        /// pending calls to <see cref="IConsoleOperations.ReadKeyAsync(bool, CancellationToken)" />
        /// on Unix platforms.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> to observe.</param>
        /// <returns>
        /// A <see cref="Task{T}" /> representing the asynchronous operation. The
        /// <see cref="Task{T}.Result" /> property will return the vertical position
        /// of the console cursor.
        /// </returns>
        public static Task<int> GetCursorTopAsync(CancellationToken cancellationToken) =>
            s_consoleProxy.GetCursorTopAsync(cancellationToken);

        /// <summary>
        /// This method is sent to PSReadLine as a workaround for issues with the System.Console
        /// implementation. Functionally it is the same as System.Console.ReadKey,
        /// with the exception that it will not lock the standard input stream.
        /// </summary>
        /// <param name="intercept">
        /// Determines whether to display the pressed key in the console window.
        /// true to not display the pressed key; otherwise, false.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> that can be used to cancel the request.
        /// </param>
        /// <returns>
        /// An object that describes the ConsoleKey constant and Unicode character, if any,
        /// that correspond to the pressed console key. The ConsoleKeyInfo object also describes,
        /// in a bitwise combination of ConsoleModifiers values, whether one or more Shift, Alt,
        /// or Ctrl modifier keys was pressed simultaneously with the console key.
        /// </returns>
        internal static ConsoleKeyInfo SafeReadKey(bool intercept, CancellationToken cancellationToken)
        {
            try
            {
                return s_consoleProxy.ReadKey(intercept, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new ConsoleKeyInfo(
                    keyChar: ' ',
                    ConsoleKey.DownArrow,
                    shift: false,
                    alt: false,
                    control: false);
            }
        }
    }
}
