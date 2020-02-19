//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Contains details about an executed
    /// </summary>
    internal class ExecutionStatusChangedEventArgs
    {
        #region Properties

        /// <summary>
        /// Gets the options used when the command was executed.
        /// </summary>
        public ExecutionOptions ExecutionOptions { get; private set; }

        /// <summary>
        /// Gets the command execution's current status.
        /// </summary>
        public ExecutionStatus ExecutionStatus { get; private set; }

        /// <summary>
        /// If true, the command execution had errors.
        /// </summary>
        public bool HadErrors { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ExecutionStatusChangedEventArgs class.
        /// </summary>
        /// <param name="executionStatus">The command execution's current status.</param>
        /// <param name="executionOptions">The options used when the command was executed.</param>
        /// <param name="hadErrors">If execution has completed, indicates whether there were errors.</param>
        public ExecutionStatusChangedEventArgs(
            ExecutionStatus executionStatus,
            ExecutionOptions executionOptions,
            bool hadErrors)
        {
            this.ExecutionStatus = executionStatus;
            this.ExecutionOptions = executionOptions;
            this.HadErrors = hadErrors || (executionStatus == ExecutionStatus.Failed);
        }

        #endregion
    }
}
