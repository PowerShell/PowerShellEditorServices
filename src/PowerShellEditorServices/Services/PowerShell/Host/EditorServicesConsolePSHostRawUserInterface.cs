using System;
using System.Collections.Generic;
using System.Management.Automation.Host;
using System.Text;

namespace PowerShellEditorServices.Services.PowerShell.Host
{
    internal class EditorServicesConsolePSHostRawUserInterface : PSHostRawUserInterface
    {
        public override ConsoleColor BackgroundColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override Size BufferSize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override Coordinates CursorPosition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override int CursorSize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override ConsoleColor ForegroundColor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override bool KeyAvailable => throw new NotImplementedException();

        public override Size MaxPhysicalWindowSize => throw new NotImplementedException();

        public override Size MaxWindowSize => throw new NotImplementedException();

        public override Coordinates WindowPosition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override Size WindowSize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string WindowTitle { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void FlushInputBuffer()
        {
            throw new NotImplementedException();
        }

        public override BufferCell[,] GetBufferContents(Rectangle rectangle)
        {
            throw new NotImplementedException();
        }

        public override KeyInfo ReadKey(ReadKeyOptions options)
        {
            throw new NotImplementedException();
        }

        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill)
        {
            throw new NotImplementedException();
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
            throw new NotImplementedException();
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
            throw new NotImplementedException();
        }
    }
}
