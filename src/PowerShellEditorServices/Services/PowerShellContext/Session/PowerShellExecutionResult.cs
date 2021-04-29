// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Enumerates the possible execution results that can occur after
    /// executing a command or script.
    /// </summary>
    internal enum PowerShellExecutionResult
    {
        /// <summary>
        /// Indicates that execution is not yet finished.
        /// </summary>
        NotFinished,

        /// <summary>
        /// Indicates that execution has failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Indicates that execution was aborted by the user.
        /// </summary>
        Aborted,

        /// <summary>
        /// Indicates that execution was stopped by the debugger.
        /// </summary>
        Stopped,

        /// <summary>
        /// Indicates that execution completed successfully.
        /// </summary>
        Completed
    }
}
