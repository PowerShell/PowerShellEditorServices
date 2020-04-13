//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation;
using System.Runtime.CompilerServices;

namespace Microsoft.PowerShell.EditorServices.Commands
{
    /// <summary>
    /// The Start-EditorServices command, the conventional entrypoint for PowerShell Editor Services.
    /// </summary>
    public sealed class InvokeReadLineConstructorCommand : PSCmdlet
    {
        protected override void EndProcessing()
        {
            Type type = Type.GetType("Microsoft.PowerShell.PSConsoleReadLine, Microsoft.PowerShell.PSReadLine2");
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
        }
    }
}
