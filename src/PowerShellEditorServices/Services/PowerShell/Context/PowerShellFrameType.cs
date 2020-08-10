using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    [Flags]
    internal enum PowerShellFrameType
    {
        Normal = 0x0,
        Nested = 0x1,
        Debug = 0x2,
        Remote = 0x4,
        NonInteractive = 0x8,
    }
}
