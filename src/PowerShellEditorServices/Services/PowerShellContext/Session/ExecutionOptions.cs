//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Defines options for the execution of a command.
    /// </summary>
    internal class ExecutionOptions
    {
        private bool? _shouldExecuteInOriginalRunspace;

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

        /// <summary>
        /// Gets or sets a value indicating whether the text of the command
        /// should be written to the host as if it was ran interactively.
        /// </summary>
        public bool WriteInputToHost { get; set; }

        /// <summary>
        /// If this is set, we will use this string for history and writing to the host
        /// instead of grabbing the command from the PSCommand.
        /// </summary>
        public string InputString { get; set; }

        /// <summary>
        /// If this is set, we will use this string for history and writing to the host
        /// instead of grabbing the command from the PSCommand.
        /// </summary>
        public bool UseNewScope { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the command to
        /// be executed is a console input prompt, such as the
        /// PSConsoleHostReadLine function.
        /// </summary>
        internal bool IsReadLine { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the command should
        /// be invoked in the original runspace. In the majority of cases
        /// this should remain unset.
        /// </summary>
        internal bool ShouldExecuteInOriginalRunspace
        {
            get
            {
                return _shouldExecuteInOriginalRunspace ?? IsReadLine;
            }
            set
            {
                _shouldExecuteInOriginalRunspace = value;
            }
        }

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
            this.WriteInputToHost = false;
            this.AddToHistory = false;
            this.InterruptCommandPrompt = false;
        }

        #endregion
    }
}
