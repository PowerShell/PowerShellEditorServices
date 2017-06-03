//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Provides a standard implementation of ChoicePromptHandler
    /// for use in the interactive console (REPL).
    /// </summary>
    public class ConsoleChoicePromptHandler : ChoicePromptHandler
    {
        #region Private Fields

        private IConsoleHost consoleHost;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates an instance of the ConsoleChoicePromptHandler class.
        /// </summary>
        /// <param name="consoleHost">
        /// The IConsoleHost implementation to use for writing to the
        /// console.
        /// </param>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public ConsoleChoicePromptHandler(IConsoleHost consoleHost, ILogger logger)
            : base(logger)
        {
            this.consoleHost = consoleHost;
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
                    this.consoleHost.WriteOutput(this.Caption);
                }

                if (this.Message != null)
                {
                    this.consoleHost.WriteOutput(this.Message);
                }
            }

            foreach (var choice in this.Choices)
            {
                string hotKeyString =
                    choice.HotKeyIndex > -1 ?
                        choice.Label[choice.HotKeyIndex].ToString().ToUpper() :
                        string.Empty;

                this.consoleHost.WriteOutput(
                    string.Format(
                        "[{0}] {1} ",
                        hotKeyString,
                        choice.Label),
                    false);
            }

            this.consoleHost.WriteOutput("[?] Help", false);

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

                this.consoleHost.WriteOutput(
                    $" (default is \"{choiceString}\"): ",
                    false);
            }
        }

        /// <summary>
        /// Reads an input string from the user.
        /// </summary>
        /// <param name="cancellationToken">A CancellationToken that can be used to cancel the prompt.</param>
        /// <returns>A Task that can be awaited to get the user's response.</returns>
        protected override Task<string> ReadInputString(CancellationToken cancellationToken)
        {
            return this.consoleHost.ReadSimpleLine(cancellationToken);
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
                    this.consoleHost.WriteOutput(
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

