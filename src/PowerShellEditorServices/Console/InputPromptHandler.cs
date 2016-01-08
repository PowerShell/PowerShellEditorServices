//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Console
{
    /// <summary>
    /// Provides a base implementation for IPromptHandler classes 
    /// that present the user a set of fields for which values
    /// should be entered.
    /// </summary>
    public abstract class InputPromptHandler : IPromptHandler
    {
        #region Private Fields

        private int currentFieldIndex;
        private FieldDetails currentField;
        private TaskCompletionSource<Dictionary<string, object>> promptTask;
        private Dictionary<string, object> fieldValues = new Dictionary<string, object>();

        private int currentCollectionIndex;
        private FieldDetails currentCollectionField;
        private ArrayList currentCollectionItems;

        #endregion

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
        public Task<string> PromptForInput()
        {
            Task<Dictionary<string, object>> innerTask =
                this.PromptForInput(
                    null,
                    null,
                    new FieldDetails[] { new FieldDetails("", "", typeof(string), false, "") });

            return 
                innerTask.ContinueWith<string>(
                    task =>
                    {
                        if (task.IsFaulted)
                        {
                            throw task.Exception;
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
        /// <returns>
        /// A Task instance that can be monitored for completion to get
        /// the user's input.
        /// </returns>
        public Task<Dictionary<string, object>> PromptForInput(
            string promptCaption,
            string promptMessage,
            FieldDetails[] fields)
        {
            this.promptTask = new TaskCompletionSource<Dictionary<string, object>>();

            this.Fields = fields;

            this.ShowPromptMessage(promptCaption, promptMessage);
            this.ShowNextPrompt();

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
        public bool HandleResponse(string responseString)
        {
            if (this.currentField == null)
            {
                // TODO: Assert
            }

            // TODO: Is string empty?  Use default or finish prompt?
            object responseValue = responseString;

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
                // Show an error and redisplay the same field
                this.ShowErrorMessage(e.InnerException);
                this.ShowFieldPrompt(this.currentField);
                return false;
            }

            if (this.currentCollectionField != null)
            {
                if (responseString.Length == 0)
                {
                    object collection = this.currentCollectionItems;

                    // Should the result collection be an array?
                    if (this.currentCollectionField.FieldType.IsArray)
                    {
                        // Convert the ArrayList to an array
                        collection =
                            this.currentCollectionItems.ToArray(
                                this.currentCollectionField.ElementType);
                    }

                    // Collection entry is done, save the items and clean up state
                    this.fieldValues.Add(
                        this.currentCollectionField.Name,
                        collection);

                    this.currentField = this.currentCollectionField;
                    this.currentCollectionField = null;
                    this.currentCollectionItems = null;
                }
                else
                {
                    // Add the item to the collection
                    this.currentCollectionItems.Add(responseValue);
                }
            }
            else
            {
                this.fieldValues.Add(this.currentField.Name, responseValue);
            }

            // If there are no more fields to show the prompt is complete
            if (this.ShowNextPrompt() == false)
            {
                this.promptTask.SetResult(this.fieldValues);
                return true;
            }

            // Prompt is still active
            return false;
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

        private bool ShowNextPrompt()
        {
            if (this.currentCollectionField != null)
            {
                // Continuing collection entry
                this.currentCollectionIndex++;
                this.currentField.Name =
                    string.Format(
                        "{0}[{1}]",
                        this.currentCollectionField.Name,
                        this.currentCollectionIndex);
            }
            else
            {
                // Have we shown all the prompts already?
                if (this.currentFieldIndex >= this.Fields.Length)
                {
                    return false;
                }

                this.currentField = this.Fields[this.currentFieldIndex];

                if (this.currentField.IsCollection)
                {
                    this.currentCollectionIndex = 0;
                    this.currentCollectionField = this.currentField;
                    this.currentCollectionItems = new ArrayList();

                    this.currentField =
                        new FieldDetails(
                            string.Format(
                                "{0}[{1}]",
                                this.currentCollectionField.Name,
                                this.currentCollectionIndex),
                            this.currentCollectionField.Label,
                            this.currentCollectionField.ElementType,
                            true,
                            null);
                }

                this.currentFieldIndex++;
            }

            this.ShowFieldPrompt(this.currentField);
            return true;
        }

        #endregion
    }
}

