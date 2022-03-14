// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
