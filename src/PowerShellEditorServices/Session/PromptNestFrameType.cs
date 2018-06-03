using System;

namespace Microsoft.PowerShell.EditorServices.Session
{
    [Flags]
    internal enum PromptNestFrameType
    {
        Normal = 0,

        NestedPrompt = 1,

        Debug = 2,

        Remote = 4
    }
}
