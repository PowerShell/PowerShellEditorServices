//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides an interface that must be implemented by an editor
    /// host to perform operations invoked by extensions written in
    /// PowerShell.
    /// </summary>
    public interface IEditorOperations
    {
        /// <summary>
        /// Gets the EditorContext for the editor's current state.
        /// </summary>
        /// <returns>A new EditorContext object.</returns>
        Task<EditorContext> GetEditorContext();

        /// <summary>
        /// Causes a file to be opened in the editor.  If the file is
        /// already open, the editor must switch to the file.
        /// </summary>
        /// <param name="filePath">The path of the file to be opened.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task OpenFile(string filePath);

        /// <summary>
        /// Inserts text into the specified range for the file at the specified path.
        /// </summary>
        /// <param name="filePath">The path of the file which will have text inserted.</param>
        /// <param name="insertText">The text to insert into the file.</param>
        /// <param name="insertRange">The range in the file to be replaced.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task InsertText(string filePath, string insertText, BufferRange insertRange);

        /// <summary>
        /// Causes the selection to be changed in the editor's active file buffer.
        /// </summary>
        /// <param name="selectionRange">The range over which the selection will be made.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task SetSelection(BufferRange selectionRange);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task ShowInformationMessage(string message);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task ShowErrorMessage(string message);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task ShowWarningMessage(string message);
    }
}

