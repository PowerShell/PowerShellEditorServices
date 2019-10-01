//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
