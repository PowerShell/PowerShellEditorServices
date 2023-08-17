// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides a PowerShell-facing API which allows scripts to
    /// interact with the editor's workspace.
    /// </summary>
    public sealed class EditorWorkspace
    {
        #region Private Fields

        private readonly IEditorOperations editorOperations;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the server's initial working directory, since the extension API doesn't have a
        /// multi-root workspace concept.
        /// </summary>
        public string Path => editorOperations.GetWorkspacePath();

        /// <summary>
        /// Get all the workspace folders' paths.
        /// </summary>
        public string[] Paths => editorOperations.GetWorkspacePaths();

        #endregion

        #region Constructors

        internal EditorWorkspace(IEditorOperations editorOperations) => this.editorOperations = editorOperations;

        #endregion

        #region Public Methods
        // TODO: Consider returning bool instead of void to indicate success?

        /// <summary>
        /// Creates a new file in the editor.
        /// </summary>
        /// <param name="content">The content to place in the new file.</param>
        public void NewFile(string content = "") => editorOperations.NewFileAsync(content).Wait();

        /// <summary>
        /// Opens a file in the workspace. If the file is already open
        /// its buffer will be made active.
        /// </summary>
        /// <param name="filePath">The path to the file to be opened.</param>
        public void OpenFile(string filePath) => editorOperations.OpenFileAsync(filePath).Wait();

        /// <summary>
        /// Opens a file in the workspace. If the file is already open
        /// its buffer will be made active.
        /// You can specify whether the file opens as a preview or as a durable editor.
        /// </summary>
        /// <param name="filePath">The path to the file to be opened.</param>
        /// <param name="preview">Determines wether the file is opened as a preview or as a durable editor.</param>
        public void OpenFile(string filePath, bool preview) => editorOperations.OpenFileAsync(filePath, preview).Wait();

        /// <summary>
        /// Closes a file in the workspace.
        /// </summary>
        /// <param name="filePath">The path to the file to be closed.</param>
        public void CloseFile(string filePath) => editorOperations.CloseFileAsync(filePath).Wait();

        /// <summary>
        /// Saves an open file in the workspace.
        /// </summary>
        /// <param name="filePath">The path to the file to be saved.</param>
        public void SaveFile(string filePath) => editorOperations.SaveFileAsync(filePath).Wait();

        /// <summary>
        /// Saves a file with a new name AKA a copy.
        /// </summary>
        /// <param name="oldFilePath">The file to copy.</param>
        /// <param name="newFilePath">The file to create.</param>
        public void SaveFile(string oldFilePath, string newFilePath) => editorOperations.SaveFileAsync(oldFilePath, newFilePath).Wait();

        #endregion
    }
}
