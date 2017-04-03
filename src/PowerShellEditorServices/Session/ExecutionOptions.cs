//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Defines options for the execution of a command.
    /// </summary>
    public class ExecutionOptions
    {
        #region Properties

        /// <summary>
        /// Gets or sets a boolean that determines whether command output
        /// should be written to the host.
        /// </summary>
        public bool WriteOutputToHost { get; set; }

        /// <summary>
        /// Gets or sets a boolean that determines whether command errors
        /// should be written to the host.
        /// </summary>
        public bool WriteErrorsToHost { get; set; }

        /// <summary>
        /// Gets or sets a boolean that determines whether the executed
        /// command should be added to the command history.
        /// </summary>
        public bool AddToHistory { get; set; }

        /// <summary>
        /// Gets or sets a boolean that determines whether the execution
        /// of the command should interrupt the command prompt.  Should
        /// only be set if WriteOutputToHost is false but the command
        /// should still interrupt the command prompt.
        /// </summary>
        public bool InterruptCommandPrompt { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ExecutionOptions class with
        /// default settings configured.
        /// </summary>
        public ExecutionOptions()
        {
            this.WriteOutputToHost = true;
            this.WriteErrorsToHost = true;
            this.AddToHistory = false;
            this.InterruptCommandPrompt = false;
        }

        #endregion
    }
}
