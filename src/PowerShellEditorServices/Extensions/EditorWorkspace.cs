// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// A document currently open in the editor workspace.
    /// </summary>
    public sealed class EditorWorkspaceDocument
    {
        private readonly EditorWorkspace _workspace;

        internal EditorWorkspaceDocument(EditorWorkspace workspace, string path, bool saved)
        {
            _workspace = workspace;
            Path = path;
            Saved = saved;
        }

        /// <summary>
        /// Gets the path of the document.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets whether the document has unsaved changes.
        /// </summary>
        public bool Saved { get; }

        /// <summary>
        /// Gets the display name of this document and unsaved status.
        /// </summary>
        /// <returns>The display name of this document.</returns>
        public override string ToString()
        {
            string fileName = System.IO.Path.GetFileName(Path);
            return Saved ? fileName : fileName + " [Unsaved]";
        }

        /// <summary>
        /// Opens this document in the editor.
        /// </summary>
        public void Open() => _workspace.OpenFile(Path);

        /// <summary>
        /// Saves this document in the editor.
        /// </summary>
        public void Save() => _workspace.SaveFile(Path);

        /// <summary>
        /// Closes this document in the editor.
        /// </summary>
        public void Close() => _workspace.CloseFile(Path);
    }

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

        /// <summary>
        /// Get all currently open documents in the workspace.
        /// </summary>
        public EditorWorkspaceDocument[] Documents
        {
            get
            {
                WorkspaceOpenDocument[] openDocuments = editorOperations.GetWorkspaceOpenDocuments();
                EditorWorkspaceDocument[] documents = new EditorWorkspaceDocument[openDocuments.Length];
                for (int i = 0; i < openDocuments.Length; i++)
                {
                    documents[i] = new EditorWorkspaceDocument(this, openDocuments[i].Path, openDocuments[i].Saved);
                }

                return documents;
            }
        }

        #endregion

        #region Constructors

        internal EditorWorkspace(IEditorOperations editorOperations) => this.editorOperations = editorOperations;

        #endregion

        #region Public Methods
        // TODO: Consider returning bool instead of void to indicate success?

        /// <summary>
        /// Creates a new file in the editor.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void NewFile() => editorOperations.NewFileAsync(string.Empty).Wait();

        /// <summary>
        /// Creates a new file in the editor.
        /// </summary>
        /// <param name="content">The content to place in the new file.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void NewFile(string content) => editorOperations.NewFileAsync(content).Wait();

        /// <summary>
        /// Opens a file in the workspace. If the file is already open
        /// its buffer will be made active.
        /// </summary>
        /// <param name="filePath">The path to the file to be opened.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void OpenFile(string filePath) => editorOperations.OpenFileAsync(filePath).Wait();

        /// <summary>
        /// Opens a file in the workspace. If the file is already open
        /// its buffer will be made active.
        /// You can specify whether the file opens as a preview or as a durable editor.
        /// </summary>
        /// <param name="filePath">The path to the file to be opened.</param>
        /// <param name="preview">Determines wether the file is opened as a preview or as a durable editor.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void OpenFile(string filePath, bool preview) => editorOperations.OpenFileAsync(filePath, preview).Wait();

        /// <summary>
        /// Closes a file in the workspace.
        /// </summary>
        /// <param name="filePath">The path to the file to be closed.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void CloseFile(string filePath) => editorOperations.CloseFileAsync(filePath).Wait();

        /// <summary>
        /// Saves an open file in the workspace.
        /// </summary>
        /// <param name="filePath">The path to the file to be saved.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void SaveFile(string filePath) => editorOperations.SaveFileAsync(filePath).Wait();

        /// <summary>
        /// Saves a file with a new name AKA a copy.
        /// </summary>
        /// <param name="oldFilePath">The file to copy.</param>
        /// <param name="newFilePath">The file to create.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Supporting synchronous API.")]
        public void SaveFile(string oldFilePath, string newFilePath) => editorOperations.SaveFileAsync(oldFilePath, newFilePath).Wait();

        #endregion
    }
}
