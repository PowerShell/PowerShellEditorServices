//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides an implementation of the PSHostRawUserInterface class
    /// for the ConsoleService and routes its calls to an IConsoleHost
    /// implementation.
    /// </summary>
    internal class TerminalPSHostRawUserInterface : PSHostRawUserInterface
    {
        #region Private Fields

        private readonly PSHostRawUserInterface internalRawUI;
        private ILogger Logger;
        private KeyInfo? lastKeyDown;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the TerminalPSHostRawUserInterface
        /// class with the given IConsoleHost implementation.
        /// </summary>
        /// <param name="logger">The ILogger implementation to use for this instance.</param>
        /// <param name="internalHost">The InternalHost instance from the origin runspace.</param>
        public TerminalPSHostRawUserInterface(ILogger logger, PSHost internalHost)
        {
            this.Logger = logger;
            this.internalRawUI = internalHost.UI.RawUI;
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
            get => this.internalRawUI.BufferSize;
            set => this.internalRawUI.BufferSize = value;
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

            set => this.internalRawUI.CursorPosition = value;
        }

        /// <summary>
        /// Gets or sets the size of the cursor in the console buffer.
        /// </summary>
        public override int CursorSize
        {
            get => this.internalRawUI.CursorSize;
            set => this.internalRawUI.CursorSize = value;
        }

        /// <summary>
        /// Gets or sets the position of the console's window.
        /// </summary>
        public override Coordinates WindowPosition
        {
            get => this.internalRawUI.WindowPosition;
            set => this.internalRawUI.WindowPosition = value;
        }

        /// <summary>
        /// Gets or sets the size of the console's window.
        /// </summary>
        public override Size WindowSize
        {
            get => this.internalRawUI.WindowSize;
            set => this.internalRawUI.WindowSize = value;
        }

        /// <summary>
        /// Gets or sets the console window's title.
        /// </summary>
        public override string WindowTitle
        {
            get => this.internalRawUI.WindowTitle;
            set => this.internalRawUI.WindowTitle = value;
        }

        /// <summary>
        /// Gets a boolean that determines whether a keypress is available.
        /// </summary>
        public override bool KeyAvailable => this.internalRawUI.KeyAvailable;

        /// <summary>
        /// Gets the maximum physical size of the console window.
        /// </summary>
        public override Size MaxPhysicalWindowSize => this.internalRawUI.MaxPhysicalWindowSize;

        /// <summary>
        /// Gets the maximum size of the console window.
        /// </summary>
        public override Size MaxWindowSize => this.internalRawUI.MaxWindowSize;

        /// <summary>
        /// Reads the current key pressed in the console.
        /// </summary>
        /// <param name="options">Options for reading the current keypress.</param>
        /// <returns>A KeyInfo struct with details about the current keypress.</returns>
        public override KeyInfo ReadKey(ReadKeyOptions options)
        {

            bool includeUp = (options & ReadKeyOptions.IncludeKeyUp) != 0;

            // Key Up was requested and we have a cached key down we can return.
            if (includeUp && this.lastKeyDown != null)
            {
                KeyInfo info = this.lastKeyDown.Value;
                this.lastKeyDown = null;
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
            Logger.LogWarning(
                "PSHostRawUserInterface.FlushInputBuffer was called");
        }

        /// <summary>
        /// Gets the contents of the console buffer in a rectangular area.
        /// </summary>
        /// <param name="rectangle">The rectangle inside which buffer contents will be accessed.</param>
        /// <returns>A BufferCell array with the requested buffer contents.</returns>
        public override BufferCell[,] GetBufferContents(Rectangle rectangle)
        {
            return this.internalRawUI.GetBufferContents(rectangle);
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
            this.internalRawUI.ScrollBufferContents(source, destination, clip, fill);
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

            this.internalRawUI.SetBufferContents(rectangle, fill);
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
            this.internalRawUI.SetBufferContents(origin, contents);
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
                this.lastKeyDown = result;
            }

            return result;
        }
    }
}
