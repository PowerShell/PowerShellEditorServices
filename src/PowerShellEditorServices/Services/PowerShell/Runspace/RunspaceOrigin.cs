//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    /// <summary>
    /// Specifies the context in which the runspace was encountered.
    /// </summary>
    internal enum RunspaceOrigin
    {
        /// <summary>
        /// The original runspace in a local session.
        /// </summary>
        Local,

        /// <summary>
        /// A remote runspace entered through Enter-PSSession.
        /// </summary>
        PSSession,

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
