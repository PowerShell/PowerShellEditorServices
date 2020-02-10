//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Indicates the style of prompt to be displayed.
    /// </summary>
    internal enum PromptStyle
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
    internal abstract class ChoicePromptHandler : PromptHandler
    {
        #region Private Fields

        private CancellationTokenSource promptCancellationTokenSource =
            new CancellationTokenSource();
        private TaskCompletionSource<Dictionary<string, object>> cancelTask =
            new TaskCompletionSource<Dictionary<string, object>>();

        #endregion

        /// <summary>
        ///
        /// </summary>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public ChoicePromptHandler(ILogger logger) : base(logger)
        {
        }

        #region Properties

        /// <summary>
        /// Returns true if the choice prompt allows multiple selections.
        /// </summary>
        protected bool IsMultiChoice { get; private set; }

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
        protected int[] DefaultChoices { get; private set; }

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
        /// <param name="cancellationToken">
        /// A CancellationToken that can be used to cancel the prompt.
        /// </param>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's choice.
        /// </returns>
        public Task<int> PromptForChoiceAsync(
            string promptCaption,
            string promptMessage,
            ChoiceDetails[] choices,
            int defaultChoice,
            CancellationToken cancellationToken)
        {
            // TODO: Guard against multiple calls

            this.Caption = promptCaption;
            this.Message = promptMessage;
            this.Choices = choices;

            this.DefaultChoices =
                defaultChoice == -1
                ? Array.Empty<int>()
                : new int[] { defaultChoice };

            // Cancel the TaskCompletionSource if the caller cancels the task
            cancellationToken.Register(this.CancelPrompt, true);

            // Convert the int[] result to int
            return this.WaitForTaskAsync(
                this.StartPromptLoopAsync(this.promptCancellationTokenSource.Token)
                    .ContinueWith(
                        task =>
                        {
                            if (task.IsFaulted)
                            {
                                throw task.Exception;
                            }
                            else if (task.IsCanceled)
                            {
                                throw new TaskCanceledException(task);
                            }

                            return this.GetSingleResult(task.GetAwaiter().GetResult());
                        }));
        }

        /// <summary>
        /// Prompts the user to make a choice of one or more options using the
        /// provided details.
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
        /// <param name="defaultChoices">
        /// The default choice(s) to highlight for the user.
        /// </param>
        /// <param name="cancellationToken">
        /// A CancellationToken that can be used to cancel the prompt.
        /// </param>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's choices.
        /// </returns>
        public Task<int[]> PromptForChoiceAsync(
            string promptCaption,
            string promptMessage,
            ChoiceDetails[] choices,
            int[] defaultChoices,
            CancellationToken cancellationToken)
        {
            // TODO: Guard against multiple calls

            this.Caption = promptCaption;
            this.Message = promptMessage;
            this.Choices = choices;
            this.DefaultChoices = defaultChoices;
            this.IsMultiChoice = true;

            // Cancel the TaskCompletionSource if the caller cancels the task
            cancellationToken.Register(this.CancelPrompt, true);

            return this.WaitForTaskAsync(
                this.StartPromptLoopAsync(
                    this.promptCancellationTokenSource.Token));
        }

        private async Task<T> WaitForTaskAsync<T>(Task<T> taskToWait)
        {
            _ = await Task.WhenAny(cancelTask.Task, taskToWait).ConfigureAwait(false);

            if (this.cancelTask.Task.IsCanceled)
            {
                throw new PipelineStoppedException();
            }

            return await taskToWait.ConfigureAwait(false);
        }

        private async Task<int[]> StartPromptLoopAsync(
            CancellationToken cancellationToken)
        {
            int[] choiceIndexes = null;

            // Show the prompt to the user
            this.ShowPrompt(PromptStyle.Full);

            while (!cancellationToken.IsCancellationRequested)
            {
                string responseString = await ReadInputStringAsync(cancellationToken).ConfigureAwait(false);
                if (responseString == null)
                {
                    // If the response string is null, the prompt has been cancelled
                    break;
                }

                choiceIndexes = this.HandleResponse(responseString);

                // Return the default choice values if no choices were entered
                if (choiceIndexes == null && string.IsNullOrEmpty(responseString))
                {
                    choiceIndexes = this.DefaultChoices;
                }

                // If the user provided no choices, we should prompt again
                if (choiceIndexes != null)
                {
                    break;
                }

                // The user did not respond with a valid choice,
                // show the prompt again to give another chance
                this.ShowPrompt(PromptStyle.Minimal);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // Throw a TaskCanceledException to stop the pipeline
                throw new TaskCanceledException();
            }

            return choiceIndexes?.ToArray();
        }

        /// <summary>
        /// Implements behavior to handle the user's response.
        /// </summary>
        /// <param name="responseString">The string representing the user's response.</param>
        /// <returns>
        /// True if the prompt is complete, false if the prompt is
        /// still waiting for a valid response.
        /// </returns>
        protected virtual int[] HandleResponse(string responseString)
        {
            List<int> choiceIndexes = new List<int>();

            // Clean up the response string and split it
            var choiceStrings =
                responseString.Trim().Split(
                    new char[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string choiceString in choiceStrings)
            {
                for (int i = 0; i < this.Choices.Length; i++)
                {
                    if (this.Choices[i].MatchesInput(choiceString))
                    {
                        choiceIndexes.Add(i);

                        // If this is a single-choice prompt, break out after
                        // the first matched choice
                        if (!this.IsMultiChoice)
                        {
                            break;
                        }
                    }
                }
            }

            if (choiceIndexes.Count == 0)
            {
                // The user did not respond with a valid choice,
                // show the prompt again to give another chance
                return null;
            }

            return choiceIndexes.ToArray();
        }

        /// <summary>
        /// Called when the active prompt should be cancelled.
        /// </summary>
        protected override void OnPromptCancelled()
        {
            // Cancel the prompt task
            this.promptCancellationTokenSource.Cancel();
            this.cancelTask.TrySetCanceled();
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

        /// <summary>
        /// Reads an input string asynchronously from the console.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken that can be used to cancel the read.
        /// </param>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's input.
        /// </returns>
        protected abstract Task<string> ReadInputStringAsync(CancellationToken cancellationToken);

        #endregion

        #region Private Methods

        private int GetSingleResult(int[] choiceArray)
        {
            return
                choiceArray != null
                ? choiceArray.DefaultIfEmpty(-1).First()
                : -1;
        }

        #endregion
    }
}
