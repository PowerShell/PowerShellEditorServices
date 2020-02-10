//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides a standard implementation of InputPromptHandler
    /// for use in the interactive console (REPL).
    /// </summary>
    internal abstract class ConsoleInputPromptHandler : InputPromptHandler
    {
        #region Private Fields

        /// <summary>
        /// The IHostOutput instance to use for this prompt.
        /// </summary>
        protected IHostOutput hostOutput;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ConsoleInputPromptHandler class.
        /// </summary>
        /// <param name="hostOutput">
        /// The IHostOutput implementation to use for writing to the
        /// console.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public ConsoleInputPromptHandler(
            IHostOutput hostOutput,
            ILogger logger)
                : base(logger)
        {
            this.hostOutput = hostOutput;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Called when the prompt caption and message should be
        /// displayed to the user.
        /// </summary>
        /// <param name="caption">The caption string to be displayed.</param>
        /// <param name="message">The message string to be displayed.</param>
        protected override void ShowPromptMessage(string caption, string message)
        {
            if (!string.IsNullOrEmpty(caption))
            {
                this.hostOutput.WriteOutput(caption, true);
            }

            if (!string.IsNullOrEmpty(message))
            {
                this.hostOutput.WriteOutput(message, true);
            }
        }

        /// <summary>
        /// Called when a prompt should be displayed for a specific
        /// input field.
        /// </summary>
        /// <param name="fieldDetails">The details of the field to be displayed.</param>
        protected override void ShowFieldPrompt(FieldDetails fieldDetails)
        {
            // For a simple prompt there won't be any field name.
            // In this case don't write anything
            if (!string.IsNullOrEmpty(fieldDetails.Name))
            {
                this.hostOutput.WriteOutput(
                    fieldDetails.Name + ": ",
                    false);
            }
        }

        /// <summary>
        /// Called when an error should be displayed, such as when the
        /// user types in a string with an incorrect format for the
        /// current field.
        /// </summary>
        /// <param name="e">
        /// The Exception containing the error to be displayed.
        /// </param>
        protected override void ShowErrorMessage(Exception e)
        {
            this.hostOutput.WriteOutput(
                e.Message,
                true,
                OutputType.Error);
        }

        #endregion
    }
}
