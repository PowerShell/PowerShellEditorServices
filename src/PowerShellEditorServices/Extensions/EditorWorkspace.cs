//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides a PowerShell-facing API which allows scripts to
    /// interact with the editor's workspace.
    /// </summary>
    public class EditorWorkspace
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
            this.editorOperations.NewFile().Wait();
        }

        /// <summary>
        /// Opens a file in the workspace.  If the file is already open
        /// its buffer will be made active.
        /// </summary>
        /// <param name="filePath">The path to the file to be opened.</param>
        public void OpenFile(string filePath)
        {
            this.editorOperations.OpenFile(filePath).Wait();
        }

        #endregion
    }
}

