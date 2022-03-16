// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides a PowerShell-facing API which allows scripts to
    /// interact with the editor's terminal.
    /// </summary>
    public class EditorTerminal
    {
        #region Private Fields

        private readonly IEditorOperations editorOperations;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the EditorTerminal class.
        /// </summary>
        /// <param name="editorOperations">An IEditorOperations implementation which handles operations in the host editor.</param>
        internal EditorTerminal(IEditorOperations editorOperations) => this.editorOperations = editorOperations;

        #endregion

        #region Public Methods

        /// <summary>
        /// Triggers to the editor to clear the terminal.
        /// </summary>
        public void Clear() => editorOperations.ClearTerminal();

        #endregion
    }
}
