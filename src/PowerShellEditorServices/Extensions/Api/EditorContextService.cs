//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Extensions.Services
{
    /// <summary>
    /// Service for managing the editor context from PSES extensions.
    /// </summary>
    public interface IEditorContextService
    {
        /// <summary>
        /// Get the file context of the currently open file.
        /// </summary>
        /// <returns>The file context of the currently open file.</returns>
        Task<ILspCurrentFileContext> GetCurrentLspFileContextAsync();

        /// <summary>
        /// Open a fresh untitled file in the editor.
        /// </summary>
        /// <returns>A task that resolves when the file has been opened.</returns>
        Task OpenNewUntitledFileAsync();

        /// <summary>
        /// Open the given file in the editor.
        /// </summary>
        /// <param name="fileUri">The absolute URI to the file to open.</param>
        /// <returns>A task that resolves when the file has been opened.</returns>
        Task OpenFileAsync(Uri fileUri);

        /// <summary>
        /// Open the given file in the editor.
        /// </summary>
        /// <param name="fileUri">The absolute URI to the file to open.</param>
        /// <param name="preview">If true, open the file as a preview.</param>
        /// <returns>A task that resolves when the file is opened.</returns>
        Task OpenFileAsync(Uri fileUri, bool preview);

        /// <summary>
        /// Close the given file in the editor.
        /// </summary>
        /// <param name="fileUri">The absolute URI to the file to close.</param>
        /// <returns>A task that resolves when the file has been closed.</returns>
        Task CloseFileAsync(Uri fileUri);

        /// <summary>
        /// Save the given file in the editor.
        /// </summary>
        /// <param name="fileUri">The absolute URI of the file to save.</param>
        /// <returns>A task that resolves when the file has been saved.</returns>
        Task SaveFileAsync(Uri fileUri);

        /// <summary>
        /// Save the given file under a new name in the editor.
        /// </summary>
        /// <param name="oldFileUri">The absolute URI of the file to save.</param>
        /// <param name="newFileUri">The absolute URI of the location to save the file.</param>
        /// <returns></returns>
        Task SaveFileAsync(Uri oldFileUri, Uri newFileUri);

        /// <summary>
        /// Set the selection in the currently focused editor window.
        /// </summary>
        /// <param name="range">The range in the file to select.</param>
        /// <returns>A task that resolves when the range has been selected.</returns>
        Task SetSelectionAsync(ILspFileRange range);

        /// <summary>
        /// Insert text into a given file.
        /// </summary>
        /// <param name="fileUri">The absolute URI of the file to insert text into.</param>
        /// <param name="text">The text to insert into the file.</param>
        /// <param name="range">The range over which to insert the given text.</param>
        /// <returns>A task that resolves when the text has been inserted.</returns>
        Task InsertTextAsync(Uri fileUri, string text, ILspFileRange range);
    }

    internal class EditorContextService : IEditorContextService
    {
        private readonly ILanguageServer _languageServer;

        internal EditorContextService(
            ILanguageServer languageServer)
        {
            _languageServer = languageServer;
        }

        public async Task<ILspCurrentFileContext> GetCurrentLspFileContextAsync()
        {
            ClientEditorContext clientContext =
                await _languageServer.SendRequest<GetEditorContextRequest>(
                    "editor/getEditorContext",
                    new GetEditorContextRequest())
                .Returning<ClientEditorContext>(CancellationToken.None)
                .ConfigureAwait(false);

            return new LspCurrentFileContext(clientContext);
        }

        public Task OpenNewUntitledFileAsync()
        {
            return _languageServer.SendRequest<string>("editor/newFile", null).ReturningVoid(CancellationToken.None);
        }

        public Task OpenFileAsync(Uri fileUri) => OpenFileAsync(fileUri, preview: false);

        public Task OpenFileAsync(Uri fileUri, bool preview)
        {
            return _languageServer.SendRequest("editor/openFile", new OpenFileDetails
            {
                FilePath = fileUri.LocalPath,
                Preview = preview,
            }).ReturningVoid(CancellationToken.None);
        }

        public Task CloseFileAsync(Uri fileUri)
        {
            return _languageServer.SendRequest("editor/closeFile", fileUri.LocalPath).ReturningVoid(CancellationToken.None);
        }

        public Task SaveFileAsync(Uri fileUri) => SaveFileAsync(fileUri, null);

        public Task SaveFileAsync(Uri oldFileUri, Uri newFileUri)
        {
            return _languageServer.SendRequest("editor/saveFile", new SaveFileDetails
            {
                FilePath = oldFileUri.LocalPath,
                NewPath = newFileUri?.LocalPath,
            }).ReturningVoid(CancellationToken.None);
        }

        public Task SetSelectionAsync(ILspFileRange range)
        {
            return _languageServer.SendRequest("editor/setSelection", new SetSelectionRequest
            {
                SelectionRange = range.ToOmnisharpRange()
            }).ReturningVoid(CancellationToken.None);
        }

        public Task InsertTextAsync(Uri fileUri, string text, ILspFileRange range)
        {
            return _languageServer.SendRequest("editor/insertText", new InsertTextRequest
            {
                FilePath = fileUri.LocalPath,
                InsertText = text,
                InsertRange = range.ToOmnisharpRange(),
            }).ReturningVoid(CancellationToken.None);
        }
    }
}
