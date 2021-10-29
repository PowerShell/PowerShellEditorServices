﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHostRawUserInterface : PSHostRawUserInterface
    {
        #region Private Fields

        private readonly PSHostRawUserInterface _internalRawUI;
        private readonly ILogger _logger;
        private KeyInfo? _lastKeyDown;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the TerminalPSHostRawUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="logger">The ILogger implementation to use for this instance.</param>
        /// <param name="internalHost">The InternalHost instance from the origin runspace.</param>
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
            get { return System.Console.BackgroundColor; }
            set { System.Console.BackgroundColor = value; }
        }

        /// <summary>
        /// Gets or sets the foreground color of the console.
        /// </summary>
        public override ConsoleColor ForegroundColor
        {
            get { return System.Console.ForegroundColor; }
            set { System.Console.ForegroundColor = value; }
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
            get
            {
                return new Coordinates(
                    ConsoleProxy.GetCursorLeft(),
                    ConsoleProxy.GetCursorTop());
            }

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
        public override KeyInfo ReadKey(ReadKeyOptions options)
        {

            bool includeUp = (options & ReadKeyOptions.IncludeKeyUp) != 0;

            // Key Up was requested and we have a cached key down we can return.
            if (includeUp && _lastKeyDown != null)
            {
                KeyInfo info = _lastKeyDown.Value;
                _lastKeyDown = null;
                return new KeyInfo(
                    info.VirtualKeyCode,
                    info.Character,
                    info.ControlKeyState,
                    keyDown: false);
            }

            bool intercept = (options & ReadKeyOptions.NoEcho) != 0;
            bool includeDown = (options & ReadKeyOptions.IncludeKeyDown) != 0;
            if (!(includeDown || includeUp))
            {
                throw new PSArgumentException(
                    "Cannot read key options. To read options, set one or both of the following: IncludeKeyDown, IncludeKeyUp.",
                    nameof(options));
            }

            // Allow ControlC as input so we can emulate pipeline stop requests. We can't actually
            // determine if a stop is requested without using non-public API's.
            bool oldValue = System.Console.TreatControlCAsInput;
            try
            {
                System.Console.TreatControlCAsInput = true;
                ConsoleKeyInfo key = ConsoleProxy.ReadKey(intercept, default(CancellationToken));

                if (IsCtrlC(key))
                {
                    // Caller wants CtrlC as input so return it.
                    if ((options & ReadKeyOptions.AllowCtrlC) != 0)
                    {
                        return ProcessKey(key, includeDown);
                    }

                    // Caller doesn't want CtrlC so throw a PipelineStoppedException to emulate
                    // a real stop.  This will not show an exception to a script based caller and it
                    // will avoid having to return something like default(KeyInfo).
                    throw new PipelineStoppedException();
                }

                return ProcessKey(key, includeDown);
            }
            finally
            {
                System.Console.TreatControlCAsInput = oldValue;
            }
        }

        /// <summary>
        /// Flushes the current input buffer.
        /// </summary>
        public override void FlushInputBuffer()
        {
            _logger.LogWarning(
                "PSHostRawUserInterface.FlushInputBuffer was called");
        }

        /// <summary>
        /// Gets the contents of the console buffer in a rectangular area.
        /// </summary>
        /// <param name="rectangle">The rectangle inside which buffer contents will be accessed.</param>
        /// <returns>A BufferCell array with the requested buffer contents.</returns>
        public override BufferCell[,] GetBufferContents(Rectangle rectangle)
        {
            return _internalRawUI.GetBufferContents(rectangle);
        }

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
            BufferCell fill)
        {
            _internalRawUI.ScrollBufferContents(source, destination, clip, fill);
        }

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
            BufferCell[,] contents)
        {
            _internalRawUI.SetBufferContents(origin, contents);
        }

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
        public override int LengthInBufferCells(char source)
        {
            return _internalRawUI.LengthInBufferCells(source);
        }
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
        public override int LengthInBufferCells(string source)
        {
            return _internalRawUI.LengthInBufferCells(source);
        }

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
        public override int LengthInBufferCells(string source, int offset)
        {
            return _internalRawUI.LengthInBufferCells(source, offset);
        }

        #endregion

        /// <summary>
        /// Determines if a key press represents the input Ctrl + C.
        /// </summary>
        /// <param name="keyInfo">The key to test.</param>
        /// <returns>
        /// <see langword="true" /> if the key represents the input Ctrl + C,
        /// otherwise <see langword="false" />.
        /// </returns>
        private static bool IsCtrlC(ConsoleKeyInfo keyInfo)
        {
            // In the VSCode terminal Ctrl C is processed as virtual key code "3", which
            // is not a named value in the ConsoleKey enum.
            if ((int)keyInfo.Key == 3)
            {
                return true;
            }

            return keyInfo.Key == ConsoleKey.C && (keyInfo.Modifiers & ConsoleModifiers.Control) != 0;
        }

        /// <summary>
        /// Converts <see cref="ConsoleKeyInfo" /> objects to <see cref="KeyInfo" /> objects and caches
        /// key down events for the next key up request.
        /// </summary>
        /// <param name="key">The key to convert.</param>
        /// <param name="isDown">
        /// A value indicating whether the result should be a key down event.
        /// </param>
        /// <returns>The converted value.</returns>
        private KeyInfo ProcessKey(ConsoleKeyInfo key, bool isDown)
        {
            // Translate ConsoleModifiers to ControlKeyStates
            ControlKeyStates states = default;
            if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
            {
                states |= ControlKeyStates.LeftAltPressed;
            }

            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                states |= ControlKeyStates.LeftCtrlPressed;
            }

            if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
            {
                states |= ControlKeyStates.ShiftPressed;
            }

            var result = new KeyInfo((int)key.Key, key.KeyChar, states, isDown);
            if (isDown)
            {
                _lastKeyDown = result;
            }

            return result;
        }
    }
}
