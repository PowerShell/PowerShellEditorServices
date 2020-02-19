//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Enumerates the possible execution results that can occur after
    /// executing a command or script.
    /// </summary>
    internal enum ExecutionStatus
    {
        /// <summary>
        /// Indicates that execution has not yet started.
        /// </summary>
        Pending,

        /// <summary>
        /// Indicates that the command is executing.
        /// </summary>
        Running,

        /// <summary>
        /// Indicates that execution has failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Indicates that execution was aborted by the user.
        /// </summary>
        Aborted,

        /// <summary>
        /// Indicates that execution completed successfully.
        /// </summary>
        Completed
    }
}
