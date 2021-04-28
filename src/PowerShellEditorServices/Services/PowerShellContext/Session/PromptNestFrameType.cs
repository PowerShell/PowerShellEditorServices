// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
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
