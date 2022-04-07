// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    /// <summary>
    /// Provides asynchronous implementations of the <see cref="Console" /> API's as well as
    /// synchronous implementations that work around platform specific issues.
    /// NOTE: We're missing GetCursorPosition.
    /// </summary>
    internal static class ConsoleProxy
    {
        internal static readonly ConsoleKeyInfo s_nullKeyInfo = new(
            keyChar: ' ',
            ConsoleKey.DownArrow,
            shift: false,
            alt: false,
            control: false);

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
            ConsoleKeyInfo key = System.Console.ReadKey(intercept);
            return cancellationToken.IsCancellationRequested ? s_nullKeyInfo : key;
        }
    }
}
