// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    internal class NullPSHostRawUI : PSHostRawUserInterface
    {
        private readonly BufferCell[,] _buffer;

        public NullPSHostRawUI() => _buffer = new BufferCell[0, 0];

        public override ConsoleColor BackgroundColor { get; set; }
        public override Size BufferSize { get; set; }
        public override Coordinates CursorPosition { get; set; }
        public override int CursorSize { get; set; }
        public override ConsoleColor ForegroundColor { get; set; }

        public override bool KeyAvailable => false;

        public override Size MaxPhysicalWindowSize => MaxWindowSize;

        public override Size MaxWindowSize => new() { Width = _buffer.GetLength(0), Height = _buffer.GetLength(1) };

        public override Coordinates WindowPosition { get; set; }
        public override Size WindowSize { get; set; }
        public override string WindowTitle { get; set; }

        public override void FlushInputBuffer()
        {
            // Do nothing
        }

        public override BufferCell[,] GetBufferContents(Rectangle rectangle) => _buffer;

        public override KeyInfo ReadKey(ReadKeyOptions options) => default;

        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill)
        {
            // Do nothing
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
            // Do nothing
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
            // Do nothing
        }
    }
}
