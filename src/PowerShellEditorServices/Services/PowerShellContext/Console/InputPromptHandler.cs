//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides a base implementation for IPromptHandler classes
    /// that present the user a set of fields for which values
    /// should be entered.
    /// </summary>
    internal abstract class InputPromptHandler : PromptHandler
    {
        #region Private Fields

        private int currentFieldIndex = -1;
        private FieldDetails currentField;
        private CancellationTokenSource promptCancellationTokenSource =
            new CancellationTokenSource();
        private TaskCompletionSource<Dictionary<string, object>> cancelTask =
            new TaskCompletionSource<Dictionary<string, object>>();

        #endregion

        /// <summary>
        ///
        /// </summary>
        /// <param name="logger">An ILogger implementation used for writing log messages.</param>
        public InputPromptHandler(ILogger logger) : base(logger)
        {
        }

        #region Properties

        /// <summary>
        /// Gets the array of fields for which the user must enter values.
        /// </summary>
        protected FieldDetails[] Fields { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Prompts the user for a line of input without writing any message or caption.
        /// </summary>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's input.
        /// </returns>
        public Task<string> PromptForInputAsync(
            CancellationToken cancellationToken)
        {
            Task<Dictionary<string, object>> innerTask =
                this.PromptForInputAsync(
                    null,
                    null,
                    new FieldDetails[] { new FieldDetails("", "", typeof(string), false, "") },
                    cancellationToken);

            return
                innerTask.ContinueWith<string>(
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

                        // Return the value of the sole field
                        return (string)task.Result[""];
                    });
        }

        /// <summary>
        /// Prompts the user for a line (or lines) of input.
        /// </summary>
        /// <param name="promptCaption">
        /// A title shown before the series of input fields.
        /// </param>
        /// <param name="promptMessage">
        /// A descritpive message shown before the series of input fields.
        /// </param>
        /// <param name="fields">
        /// An array of FieldDetails items to be displayed which prompt the
        /// user for input of a specific type.
        /// </param>
        /// <param name="cancellationToken">
        /// A CancellationToken that can be used to cancel the prompt.
        /// </param>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's input.
        /// </returns>
        public async Task<Dictionary<string, object>> PromptForInputAsync(
            string promptCaption,
            string promptMessage,
            FieldDetails[] fields,
            CancellationToken cancellationToken)
        {
            // Cancel the prompt if the caller cancels the task
            cancellationToken.Register(this.CancelPrompt, true);

            this.Fields = fields;

            this.ShowPromptMessage(promptCaption, promptMessage);

            Task<Dictionary<string, object>> promptTask =
                this.StartPromptLoopAsync(this.promptCancellationTokenSource.Token);

            _ = await Task.WhenAny(cancelTask.Task, promptTask).ConfigureAwait(false);

            if (this.cancelTask.Task.IsCanceled)
            {
                throw new PipelineStoppedException();
            }

            return promptTask.Result;
        }

        /// <summary>
        /// Prompts the user for a SecureString without writing any message or caption.
        /// </summary>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's input.
        /// </returns>
        public Task<SecureString> PromptForSecureInputAsync(
            CancellationToken cancellationToken)
        {
            Task<Dictionary<string, object>> innerTask =
                this.PromptForInputAsync(
                    null,
                    null,
                    new FieldDetails[] { new FieldDetails("", "", typeof(SecureString), false, "") },
                    cancellationToken);

            return
                innerTask.ContinueWith(
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

                        // Return the value of the sole field
                        return (SecureString)task.Result?[""];
                    });
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
        /// Called when the prompt caption and message should be
        /// displayed to the user.
        /// </summary>
        /// <param name="caption">The caption string to be displayed.</param>
        /// <param name="message">The message string to be displayed.</param>
        protected abstract void ShowPromptMessage(string caption, string message);

        /// <summary>
        /// Called when a prompt should be displayed for a specific
        /// input field.
        /// </summary>
        /// <param name="fieldDetails">The details of the field to be displayed.</param>
        protected abstract void ShowFieldPrompt(FieldDetails fieldDetails);

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

        /// <summary>
        /// Reads a SecureString asynchronously from the console.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken that can be used to cancel the read.
        /// </param>
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's input.
        /// </returns>
        protected abstract Task<SecureString> ReadSecureStringAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Called when an error should be displayed, such as when the
        /// user types in a string with an incorrect format for the
        /// current field.
        /// </summary>
        /// <param name="e">
        /// The Exception containing the error to be displayed.
        /// </param>
        protected abstract void ShowErrorMessage(Exception e);

        #endregion

        #region Private Methods

        private async Task<Dictionary<string, object>> StartPromptLoopAsync(
            CancellationToken cancellationToken)
        {
            this.GetNextField();

            // Loop until there are no more prompts to process
            while (this.currentField != null && !cancellationToken.IsCancellationRequested)
            {
                // Show current prompt
                this.ShowFieldPrompt(this.currentField);

                bool enteredValue = false;
                object responseValue = null;
                string responseString = null;

                // Read input depending on field type
                if (this.currentField.FieldType == typeof(SecureString))
                {
                    SecureString secureString = await this.ReadSecureStringAsync(cancellationToken).ConfigureAwait(false);
                    responseValue = secureString;
                    enteredValue = secureString != null;
                }
                else
                {
                    responseString = await this.ReadInputStringAsync(cancellationToken).ConfigureAwait(false);
                    responseValue = responseString;
                    enteredValue = responseString != null && responseString.Length > 0;

                    try
                    {
                        responseValue =
                            LanguagePrimitives.ConvertTo(
                                responseString,
                                this.currentField.FieldType,
                                CultureInfo.CurrentCulture);
                    }
                    catch (PSInvalidCastException e)
                    {
                        this.ShowErrorMessage(e.InnerException ?? e);
                        continue;
                    }
                }

                // Set the field's value and get the next field
                this.currentField.SetValue(responseValue, enteredValue);
                this.GetNextField();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // Throw a TaskCanceledException to stop the pipeline
                throw new TaskCanceledException();
            }

            // Return the field values
            return this.GetFieldValues();
        }

        private FieldDetails GetNextField()
        {
            FieldDetails nextField = this.currentField?.GetNextField();

            if (nextField == null)
            {
                this.currentFieldIndex++;

                // Have we shown all the prompts already?
                if (this.currentFieldIndex < this.Fields.Length)
                {
                    nextField = this.Fields[this.currentFieldIndex];
                }
            }

            this.currentField = nextField;
            return nextField;
        }

        private Dictionary<string, object> GetFieldValues()
        {
            Dictionary<string, object> fieldValues = new Dictionary<string, object>();

            foreach (FieldDetails field in this.Fields)
            {
                fieldValues.Add(field.OriginalName, field.GetValue(this.Logger));
            }

            return fieldValues;
        }

        #endregion
    }
}
