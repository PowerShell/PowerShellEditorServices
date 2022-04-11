// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    [Flags]
    internal enum PowerShellFrameType
    {
        Normal = 0 << 0,
        Nested = 1 << 0,
        Debug = 1 << 1,
        Remote = 1 << 2,
        NonInteractive = 1 << 3,
        Repl = 1 << 4,
    }
}
