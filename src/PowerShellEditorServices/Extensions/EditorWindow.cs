// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        public EditorTerminal Terminal { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the EditorWindow class.
        /// </summary>
        /// <param name="editorOperations">An IEditorOperations implementation which handles operations in the host editor.</param>
        internal EditorWindow(IEditorOperations editorOperations)
        {
            this.editorOperations = editorOperations;
            Terminal = new EditorTerminal(editorOperations);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows an informational message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        public void ShowInformationMessage(string message) => editorOperations.ShowInformationMessageAsync(message).Wait();

        /// <summary>
        /// Shows an error message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        public void ShowErrorMessage(string message) => editorOperations.ShowErrorMessageAsync(message).Wait();

        /// <summary>
        /// Shows a warning message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        public void ShowWarningMessage(string message) => editorOperations.ShowWarningMessageAsync(message).Wait();

        /// <summary>
        /// Sets the status bar message in the editor UI (if applicable).
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        public void SetStatusBarMessage(string message) => editorOperations.SetStatusBarMessageAsync(message, null).Wait();

        /// <summary>
        /// Sets the status bar message in the editor UI (if applicable).
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        /// <param name="timeout">A timeout in milliseconds for how long the message should remain visible.</param>
        public void SetStatusBarMessage(string message, int timeout) => editorOperations.SetStatusBarMessageAsync(message, timeout).Wait();

        #endregion
    }
}
