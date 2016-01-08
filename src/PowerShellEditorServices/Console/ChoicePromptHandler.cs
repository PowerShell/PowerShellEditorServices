//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Indicates the style of prompt to be displayed.
    /// </summary>
    public enum PromptStyle
    {
        /// <summary>
        /// Indicates that the full prompt should be displayed
        /// with all relevant details.
        /// </summary>
        Full,

        /// <summary>
        /// Indicates that a minimal prompt should be displayed,
        /// generally used after the full prompt has already been
        /// displayed and the options must be displayed again.
        /// </summary>
        Minimal
    }

    /// <summary>
    /// Provides a base implementation for IPromptHandler classes 
    /// that present the user a set of options from which a selection
    /// should be made.
    /// </summary>
    public abstract class ChoicePromptHandler : IPromptHandler
    {
        #region Private Fields

        private TaskCompletionSource<int> promptTask;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the caption (title) string to display with the prompt.
        /// </summary>
        protected string Caption { get; private set; }

        /// <summary>
        /// Gets the descriptive message to display with the prompt.
        /// </summary>
        protected string Message { get; private set; }

        /// <summary>
        /// Gets the array of choices from which the user must select.
        /// </summary>
        protected ChoiceDetails[] Choices { get; private set; }

        /// <summary>
        /// Gets the index of the default choice so that the user
        /// interface can make it easy to select this option.
        /// </summary>
        protected int DefaultChoice { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Prompts the user to make a choice using the provided details.
        /// </summary>
        /// <param name="promptCaption">
        /// The caption string which will be displayed to the user.
        /// </param>
        /// <param name="promptMessage">
        /// The descriptive message which will be displayed to the user.
        /// </param>
        /// <param name="choices">
        /// The list of choices from which the user will select.
        /// </param>
        /// <param name="defaultChoice">
        /// The default choice to highlight for the user.
        /// </param>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's choice.
        /// </returns>
        public Task<int> PromptForChoice(
            string promptCaption,
            string promptMessage,
            ChoiceDetails[] choices,
            int defaultChoice)
        {
            // TODO: Guard against multiple calls

            this.Caption = promptCaption;
            this.Message = promptMessage;
            this.Choices = choices;
            this.DefaultChoice = defaultChoice;
            this.promptTask = new TaskCompletionSource<int>();

            // Show the prompt to the user
            this.ShowPrompt(PromptStyle.Full);

            return this.promptTask.Task;
        }

        /// <summary>
        /// Implements behavior to handle the user's response.
        /// </summary>
        /// <param name="responseString">The string representing the user's response.</param>
        /// <returns>
        /// True if the prompt is complete, false if the prompt is 
        /// still waiting for a valid response.
        /// </returns>
        public virtual bool HandleResponse(string responseString)
        {
            int choiceIndex = -1;

            // Clean up the response string
            responseString = responseString.Trim();

            for (int i = 0; i < this.Choices.Length; i++)
            {
                if (this.Choices[i].MatchesInput(responseString))
                {
                    choiceIndex = i;
                    break;
                }
            }

            if (choiceIndex == -1)
            {
                // The user did not respond with a valid choice,
                // show the prompt again to give another chance
                this.ShowPrompt(PromptStyle.Minimal);
                return false;
            }

            this.promptTask.SetResult(choiceIndex);
            return true;
        }

        /// <summary>
        /// Called when the active prompt should be cancelled.
        /// </summary>
        public void CancelPrompt()
        {
            // Cancel the prompt task
            this.promptTask.TrySetCanceled();
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Called when the prompt should be displayed to the user.
        /// </summary>
        /// <param name="promptStyle">
        /// Indicates the prompt style to use when showing the prompt.
        /// </param>
        protected abstract void ShowPrompt(PromptStyle promptStyle);

        #endregion
    }
}

