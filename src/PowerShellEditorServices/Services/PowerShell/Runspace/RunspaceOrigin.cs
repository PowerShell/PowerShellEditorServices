//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    /// <summary>
    /// Specifies the context in which the runspace was encountered.
    /// </summary>
    internal enum RunspaceOrigin
    {
        /// <summary>
        /// The original runspace in a local or remote session.
        /// </summary>
        Original,

        /// <summary>
        /// A runspace in a process that was entered with Enter-PSHostProcess.
        /// </summary>
        EnteredProcess,

        /// <summary>
        /// A runspace that is being debugged with Debug-Runspace.
        /// </summary>
        DebuggedRunspace
    }
}
