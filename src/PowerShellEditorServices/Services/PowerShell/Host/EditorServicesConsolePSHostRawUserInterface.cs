// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation.Host;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHostRawUserInterface : PSHostRawUserInterface
    {
        #region Private Fields

        private readonly PSHostRawUserInterface _internalRawUI;
        private readonly ILogger _logger;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the TerminalPSHostRawUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        public EditorServicesConsolePSHostRawUserInterface(
            ILoggerFactory loggerFactory,
            PSHostRawUserInterface internalRawUI)
        {
            _logger = loggerFactory.CreateLogger<EditorServicesConsolePSHostRawUserInterface>();
            _internalRawUI = internalRawUI;
        }

        #endregion

        #region PSHostRawUserInterface Implementation

        /// <summary>
        /// Gets or sets the background color of the console.
        /// </summary>
        public override ConsoleColor BackgroundColor
        {
            get => System.Console.BackgroundColor;
            set => System.Console.BackgroundColor = value;
        }

        /// <summary>
        /// Gets or sets the foreground color of the console.
        /// </summary>
        public override ConsoleColor ForegroundColor
        {
            get => System.Console.ForegroundColor;
            set => System.Console.ForegroundColor = value;
        }

        /// <summary>
        /// Gets or sets the size of the console buffer.
        /// </summary>
        public override Size BufferSize
        {
            get => _internalRawUI.BufferSize;
            set => _internalRawUI.BufferSize = value;
        }

        /// <summary>
        /// Gets or sets the cursor's position in the console buffer.
        /// </summary>
        public override Coordinates CursorPosition
        {
            get => _internalRawUI.CursorPosition;
            set => _internalRawUI.CursorPosition = value;
        }

        /// <summary>
        /// Gets or sets the size of the cursor in the console buffer.
        /// </summary>
        public override int CursorSize
        {
            get => _internalRawUI.CursorSize;
            set => _internalRawUI.CursorSize = value;
        }

        /// <summary>
        /// Gets or sets the position of the console's window.
        /// </summary>
        public override Coordinates WindowPosition
        {
            get => _internalRawUI.WindowPosition;
            set => _internalRawUI.WindowPosition = value;
        }

        /// <summary>
        /// Gets or sets the size of the console's window.
        /// </summary>
        public override Size WindowSize
        {
            get => _internalRawUI.WindowSize;
            set => _internalRawUI.WindowSize = value;
        }

        /// <summary>
        /// Gets or sets the console window's title.
        /// </summary>
        public override string WindowTitle
        {
            get => _internalRawUI.WindowTitle;
            set => _internalRawUI.WindowTitle = value;
        }

        /// <summary>
        /// Gets a boolean that determines whether a keypress is available.
        /// </summary>
        public override bool KeyAvailable => _internalRawUI.KeyAvailable;

        /// <summary>
        /// Gets the maximum physical size of the console window.
        /// </summary>
        public override Size MaxPhysicalWindowSize => _internalRawUI.MaxPhysicalWindowSize;

        /// <summary>
        /// Gets the maximum size of the console window.
        /// </summary>
        public override Size MaxWindowSize => _internalRawUI.MaxWindowSize;

        /// <summary>
        /// Reads the current key pressed in the console.
        /// </summary>
        /// <param name="options">Options for reading the current keypress.</param>
        /// <returns>A KeyInfo struct with details about the current keypress.</returns>
        public override KeyInfo ReadKey(ReadKeyOptions options) => _internalRawUI.ReadKey(options);

        /// <summary>
        /// Flushes the current input buffer.
        /// </summary>
        public override void FlushInputBuffer() => _logger.LogWarning("PSHostRawUserInterface.FlushInputBuffer was called");

        /// <summary>
        /// Gets the contents of the console buffer in a rectangular area.
        /// </summary>
        /// <param name="rectangle">The rectangle inside which buffer contents will be accessed.</param>
        /// <returns>A BufferCell array with the requested buffer contents.</returns>
        public override BufferCell[,] GetBufferContents(Rectangle rectangle) => _internalRawUI.GetBufferContents(rectangle);

        /// <summary>
        /// Scrolls the contents of the console buffer.
        /// </summary>
        /// <param name="source">The source rectangle to scroll.</param>
        /// <param name="destination">The destination coordinates by which to scroll.</param>
        /// <param name="clip">The rectangle inside which the scrolling will be clipped.</param>
        /// <param name="fill">The cell with which the buffer will be filled.</param>
        public override void ScrollBufferContents(
            Rectangle source,
            Coordinates destination,
            Rectangle clip,
            BufferCell fill) => _internalRawUI.ScrollBufferContents(source, destination, clip, fill);

        /// <summary>
        /// Sets the contents of the buffer inside the specified rectangle.
        /// </summary>
        /// <param name="rectangle">The rectangle inside which buffer contents will be filled.</param>
        /// <param name="fill">The BufferCell which will be used to fill the requested space.</param>
        public override void SetBufferContents(
            Rectangle rectangle,
            BufferCell fill)
        {
            // If the rectangle is all -1s then it means clear the visible buffer
            if (rectangle.Top == -1 &&
                rectangle.Bottom == -1 &&
                rectangle.Left == -1 &&
                rectangle.Right == -1)
            {
                System.Console.Clear();
                return;
            }

            _internalRawUI.SetBufferContents(rectangle, fill);
        }

        /// <summary>
        /// Sets the contents of the buffer at the given coordinate.
        /// </summary>
        /// <param name="origin">The coordinate at which the buffer will be changed.</param>
        /// <param name="contents">The new contents for the buffer at the given coordinate.</param>
        public override void SetBufferContents(
            Coordinates origin,
            BufferCell[,] contents) => _internalRawUI.SetBufferContents(origin, contents);

        /// <summary>
        /// Determines the number of BufferCells a character occupies.
        /// </summary>
        /// <param name="source">
        /// The character whose length we want to know.
        /// </param>
        /// <returns>
        /// The length in buffer cells according to the original host
        /// implementation for the process.
        /// </returns>
        public override int LengthInBufferCells(char source) => _internalRawUI.LengthInBufferCells(source);

        /// <summary>
        /// Determines the number of BufferCells a string occupies.
        /// </summary>
        /// <param name="source">
        /// The string whose length we want to know.
        /// </param>
        /// <returns>
        /// The length in buffer cells according to the original host
        /// implementation for the process.
        /// </returns>
        public override int LengthInBufferCells(string source) => _internalRawUI.LengthInBufferCells(source);

        /// <summary>
        /// Determines the number of BufferCells a substring of a string occupies.
        /// </summary>
        /// <param name="source">
        /// The string whose substring length we want to know.
        /// </param>
        /// <param name="offset">
        /// Offset where the substring begins in <paramref name="source"/>
        /// </param>
        /// <returns>
        /// The length in buffer cells according to the original host
        /// implementation for the process.
        /// </returns>
        public override int LengthInBufferCells(string source, int offset) => _internalRawUI.LengthInBufferCells(source, offset);

        #endregion
    }
}
