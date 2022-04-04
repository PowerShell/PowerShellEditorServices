// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Console
{
    /// <summary>
    /// Provides platform specific console utilities.
    /// </summary>
    internal interface IConsoleOperations
    {
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
        ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken);

        /// <summary>
        /// Obtains the horizontal position of the console cursor.
        /// </summary>
        /// <returns>The horizontal position of the console cursor.</returns>
        int GetCursorLeft();

        /// <summary>
        /// Obtains the horizontal position of the console cursor. TODO: Is this still necessary?
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> to observe.</param>
        /// <returns>The horizontal position of the console cursor.</returns>
        int GetCursorLeft(CancellationToken cancellationToken);

        /// <summary>
        /// Obtains the vertical position of the console cursor. TODO: Is this still necessary?
        /// </summary>
        /// <returns>The vertical position of the console cursor.</returns>
        int GetCursorTop();

        /// <summary>
        /// Obtains the vertical position of the console cursor. TODO: Is this still necessary?
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken" /> to observe.</param>
        /// <returns>The vertical position of the console cursor.</returns>
        int GetCursorTop(CancellationToken cancellationToken);
    }
}
