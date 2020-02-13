//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Extensions
{
    /// <summary>
    /// Provides an interface that must be implemented by an editor
    /// host to perform operations invoked by extensions written in
    /// PowerShell.
    /// </summary>
    internal interface IEditorOperations
    {
        /// <summary>
        /// Gets the EditorContext for the editor's current state.
        /// </summary>
        /// <returns>A new EditorContext object.</returns>
        Task<EditorContext> GetEditorContextAsync();

        /// <summary>
        /// Gets the path to the editor's active workspace.
        /// </summary>
        /// <returns>The workspace path or null if there isn't one.</returns>
        string GetWorkspacePath();

        /// <summary>
        /// Resolves the given file path relative to the current workspace path.
        /// </summary>
        /// <param name="filePath">The file path to be resolved.</param>
        /// <returns>The resolved file path.</returns>
        string GetWorkspaceRelativePath(string filePath);

        /// <summary>
        /// Causes a new untitled file to be created in the editor.
        /// </summary>
        /// <returns>A task that can be awaited for completion.</returns>
        Task NewFileAsync();

        /// <summary>
        /// Causes a file to be opened in the editor.  If the file is
        /// already open, the editor must switch to the file.
        /// </summary>
        /// <param name="filePath">The path of the file to be opened.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task OpenFileAsync(string filePath);

        /// <summary>
        /// Causes a file to be opened in the editor.  If the file is
        /// already open, the editor must switch to the file.
        /// You can specify whether the file opens as a preview or as a durable editor.
        /// </summary>
        /// <param name="filePath">The path of the file to be opened.</param>
        /// <param name="preview">Determines wether the file is opened as a preview or as a durable editor.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task OpenFileAsync(string filePath, bool preview);

        /// <summary>
        /// Causes a file to be closed in the editor.
        /// </summary>
        /// <param name="filePath">The path of the file to be closed.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task CloseFileAsync(string filePath);

        /// <summary>
        /// Causes a file to be saved in the editor.
        /// </summary>
        /// <param name="filePath">The path of the file to be saved.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task SaveFileAsync(string filePath);

        /// <summary>
        /// Causes a file to be saved as a new file in a new editor window.
        /// </summary>
        /// <param name="oldFilePath">the path of the current file being saved</param>
        /// <param name="newFilePath">the path of the new file where the current window content will be saved</param>
        /// <returns></returns>
        Task SaveFileAsync(string oldFilePath, string newFilePath);

        /// <summary>
        /// Inserts text into the specified range for the file at the specified path.
        /// </summary>
        /// <param name="filePath">The path of the file which will have text inserted.</param>
        /// <param name="insertText">The text to insert into the file.</param>
        /// <param name="insertRange">The range in the file to be replaced.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task InsertTextAsync(string filePath, string insertText, BufferRange insertRange);

        /// <summary>
        /// Causes the selection to be changed in the editor's active file buffer.
        /// </summary>
        /// <param name="selectionRange">The range over which the selection will be made.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task SetSelectionAsync(BufferRange selectionRange);

        /// <summary>
        /// Shows an informational message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task ShowInformationMessageAsync(string message);

        /// <summary>
        /// Shows an error message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task ShowErrorMessageAsync(string message);

        /// <summary>
        /// Shows a warning message to the user.
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task ShowWarningMessageAsync(string message);

        /// <summary>
        /// Sets the status bar message in the editor UI (if applicable).
        /// </summary>
        /// <param name="message">The message to be shown.</param>
        /// <param name="timeout">If non-null, a timeout in milliseconds for how long the message should remain visible.</param>
        /// <returns>A Task that can be tracked for completion.</returns>
        Task SetStatusBarMessageAsync(string message, int? timeout);

        /// <summary>
        /// Triggers to the editor to clear the terminal.
        /// </summary>
        void ClearTerminal();
    }
}
