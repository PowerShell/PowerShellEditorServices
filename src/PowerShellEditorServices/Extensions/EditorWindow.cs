//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides a PowerShell-facing API which allows scripts to
    /// interact with the editor's window.
    /// </summary>
    public sealed class EditorWindow
    {
        #region Private Fields

        private readonly IEditorOperations editorOperations;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the terminal interface for the editor API.
        /// </summary>
        public EditorTerminal Terminal { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the EditorWindow class.
        /// </summary>
        /// <param name="editorOperations">An IEditorOperations implementation which handles operations in the host editor.</param>
        internal EditorWindow(IEditorOperations editorOperations)
        {
            this.editorOperations = editorOperations;
            this.Terminal = new EditorTerminal(editorOperations);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows an informational message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        public void ShowInformationMessage(string message)
        {
            this.editorOperations.ShowInformationMessageAsync(message).Wait();
        }

        /// <summary>
        /// Shows an error message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        public void ShowErrorMessage(string message)
        {
            this.editorOperations.ShowErrorMessageAsync(message).Wait();
        }

        /// <summary>
        /// Shows a warning message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        public void ShowWarningMessage(string message)
        {
            this.editorOperations.ShowWarningMessageAsync(message).Wait();
        }

        /// <summary>
        /// Sets the status bar message in the editor UI (if applicable).
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        public void SetStatusBarMessage(string message)
        {
            this.editorOperations.SetStatusBarMessageAsync(message, null).Wait();
        }

        /// <summary>
        /// Sets the status bar message in the editor UI (if applicable).
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        /// <param name="timeout">A timeout in milliseconds for how long the message should remain visible.</param>
        public void SetStatusBarMessage(string message, int timeout)
        {
            this.editorOperations.SetStatusBarMessageAsync(message, timeout).Wait();
        }

        #endregion
    }
}
