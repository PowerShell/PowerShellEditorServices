//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides a standard implementation of ChoicePromptHandler
    /// for use in the interactive console (REPL).
    /// </summary>
    internal abstract class ConsoleChoicePromptHandler : ChoicePromptHandler
    {
        #region Private Fields

        /// <summary>
        /// The IHostOutput instance to use for this prompt.
        /// </summary>
        protected IHostOutput hostOutput;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ConsoleChoicePromptHandler class.
        /// </summary>
        /// <param name="hostOutput">
        /// The IHostOutput implementation to use for writing to the
        /// console.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public ConsoleChoicePromptHandler(
            IHostOutput hostOutput,
            ILogger logger)
                : base(logger)
        {
            this.hostOutput = hostOutput;
        }

        #endregion

        /// <summary>
        /// Called when the prompt should be displayed to the user.
        /// </summary>
        /// <param name="promptStyle">
        /// Indicates the prompt style to use when showing the prompt.
        /// </param>
        protected override void ShowPrompt(PromptStyle promptStyle)
        {
            if (promptStyle == PromptStyle.Full)
            {
                if (this.Caption != null)
                {
                    this.hostOutput.WriteOutput(this.Caption);
                }

                if (this.Message != null)
                {
                    this.hostOutput.WriteOutput(this.Message);
                }
            }

            foreach (var choice in this.Choices)
            {
                string hotKeyString =
                    choice.HotKeyIndex > -1 ?
                        choice.Label[choice.HotKeyIndex].ToString().ToUpper() :
                        string.Empty;

                this.hostOutput.WriteOutput(
                    string.Format(
                        "[{0}] {1} ",
                        hotKeyString,
                        choice.Label),
                    false);
            }

            this.hostOutput.WriteOutput("[?] Help", false);

            var validDefaultChoices =
                this.DefaultChoices.Where(
                    choice => choice > -1 && choice < this.Choices.Length);

            if (validDefaultChoices.Any())
            {
                var choiceString =
                    string.Join(
                        ", ",
                        this.DefaultChoices
                            .Select(choice => this.Choices[choice].Label));

                this.hostOutput.WriteOutput(
                    $" (default is \"{choiceString}\"): ",
                    false);
            }
        }


        /// <summary>
        /// Implements behavior to handle the user's response.
        /// </summary>
        /// <param name="responseString">The string representing the user's response.</param>
        /// <returns>
        /// True if the prompt is complete, false if the prompt is
        /// still waiting for a valid response.
        /// </returns>
        protected override int[] HandleResponse(string responseString)
        {
            if (responseString.Trim() == "?")
            {
                // Print help text
                foreach (var choice in this.Choices)
                {
                    this.hostOutput.WriteOutput(
                        string.Format(
                            "{0} - {1}",
                            (choice.HotKeyCharacter.HasValue ?
                                choice.HotKeyCharacter.Value.ToString() :
                                choice.Label),
                            choice.HelpMessage));
                }

                return null;
            }

            return base.HandleResponse(responseString);
        }
    }
}
