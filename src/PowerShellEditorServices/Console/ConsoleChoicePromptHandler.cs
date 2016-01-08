//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
        public ConsoleChoicePromptHandler(IConsoleHost consoleHost)
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

            if (this.DefaultChoice > -1 && this.DefaultChoice < this.Choices.Length)
            {
                this.consoleHost.WriteOutput(
                    string.Format(
                        " (default is \"{0}\"):",
                        this.Choices[this.DefaultChoice].Label));
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
        public override bool HandleResponse(string responseString)
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

                // Redisplay the prompt
                this.ShowPrompt(PromptStyle.Minimal);

                return false;
            }

            return base.HandleResponse(responseString);
        }
    }
}

