//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Enumerates the possible states for a PowerShellContext.
    /// </summary>
    internal enum PowerShellContextState
    {
        /// <summary>
        /// Indicates an unknown, potentially uninitialized state.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Indicates the state where the session is starting but
        /// not yet fully initialized.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Indicates that the session is ready to accept commands
        /// for execution.
        /// </summary>
        Ready,

        /// <summary>
        /// Indicates that the session is currently running a command.
        /// </summary>
        Running,

        /// <summary>
        /// Indicates that the session is aborting the current execution.
        /// </summary>
        Aborting,

        /// <summary>
        /// Indicates that the session is already disposed and cannot
        /// accept further execution requests.
        /// </summary>
        Disposed
    }
}
