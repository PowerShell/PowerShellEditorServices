//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Represents the different API's available for executing commands.
    /// </summary>
    internal enum ExecutionTarget
    {
        /// <summary>
        /// Indicates that the command should be invoked through the PowerShell debugger.
        /// </summary>
        Debugger,

        /// <summary>
        /// Indicates that the command should be invoked via an instance of the PowerShell class.
        /// </summary>
        PowerShell,

        /// <summary>
        /// Indicates that the command should be invoked through the PowerShell engine's event manager.
        /// </summary>
        InvocationEvent
    }
}
