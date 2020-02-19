//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides a PowerShell-facing API which allows scripts to
    /// interact with the editor's workspace.
    /// </summary>
    public sealed class EditorWorkspace
    {
        #region Private Fields

        private IEditorOperations editorOperations;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current workspace path if there is one or null otherwise.
        /// </summary>
        public string Path
        {
            get { return this.editorOperations.GetWorkspacePath(); }
        }

        #endregion

        #region Constructors

        internal EditorWorkspace(IEditorOperations editorOperations)
        {
            this.editorOperations = editorOperations;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new file in the editor
        /// </summary>
        public void NewFile()
        {
            this.editorOperations.NewFileAsync().Wait();
        }

        /// <summary>
        /// Opens a file in the workspace.  If the file is already open
        /// its buffer will be made active.
        /// </summary>
        /// <param name="filePath">The path to the file to be opened.</param>
        public void OpenFile(string filePath)
        {
            this.editorOperations.OpenFileAsync(filePath).Wait();
        }

        /// <summary>
        /// Opens a file in the workspace.  If the file is already open
        /// its buffer will be made active.
        /// You can specify whether the file opens as a preview or as a durable editor.
        /// </summary>
        /// <param name="filePath">The path to the file to be opened.</param>
        /// <param name="preview">Determines wether the file is opened as a preview or as a durable editor.</param>
        public void OpenFile(string filePath, bool preview)
        {
            this.editorOperations.OpenFileAsync(filePath, preview).Wait();
        }

        #endregion
    }
}
