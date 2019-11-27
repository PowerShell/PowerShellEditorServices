//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class EditorOperationsService : IEditorOperations
    {
        private const bool DefaultPreviewSetting = true;

        private WorkspaceService _workspaceService;
        private ILanguageServer _languageServer;

        public EditorOperationsService(
            WorkspaceService workspaceService,
            ILanguageServer languageServer)
        {
            this._workspaceService = workspaceService;
            this._languageServer = languageServer;
        }

        public async Task<EditorContext> GetEditorContextAsync()
        {
            ClientEditorContext clientContext =
                await _languageServer.SendRequest<GetEditorContextRequest, ClientEditorContext>(
                    "editor/getEditorContext",
                    new GetEditorContextRequest());

            return this.ConvertClientEditorContext(clientContext);
        }

        public async Task InsertTextAsync(string filePath, string text, BufferRange insertRange)
        {
            await _languageServer.SendRequest<InsertTextRequest>("editor/insertText", new InsertTextRequest
            {
                FilePath = filePath,
                InsertText = text,
                InsertRange =
                    new Range
                    {
                        Start = new Position
                        {
                            Line = insertRange.Start.Line - 1,
                            Character = insertRange.Start.Column - 1
                        },
                        End = new Position
                        {
                            Line = insertRange.End.Line - 1,
                            Character = insertRange.End.Column - 1
                        }
                    }
            });
        }

        public async Task SetSelectionAsync(BufferRange selectionRange)
        {

            await _languageServer.SendRequest<SetSelectionRequest>("editor/setSelection", new SetSelectionRequest
            {
                SelectionRange =
                    new Range
                    {
                        Start = new Position
                        {
                            Line = selectionRange.Start.Line - 1,
                            Character = selectionRange.Start.Column - 1
                        },
                        End = new Position
                        {
                            Line = selectionRange.End.Line - 1,
                            Character = selectionRange.End.Column - 1
                        }
                    }
            });
        }

        public EditorContext ConvertClientEditorContext(
            ClientEditorContext clientContext)
        {
            ScriptFile scriptFile = _workspaceService.CreateScriptFileFromFileBuffer(
                clientContext.CurrentFilePath,
                clientContext.CurrentFileContent);

            return
                new EditorContext(
                    this,
                    scriptFile,
                    new BufferPosition(
                        (int) clientContext.CursorPosition.Line + 1,
                        (int) clientContext.CursorPosition.Character + 1),
                    new BufferRange(
                        (int) clientContext.SelectionRange.Start.Line + 1,
                        (int) clientContext.SelectionRange.Start.Character + 1,
                        (int) clientContext.SelectionRange.End.Line + 1,
                        (int) clientContext.SelectionRange.End.Character + 1),
                    clientContext.CurrentFileLanguage);
        }

        public async Task NewFileAsync()
        {
            await _languageServer.SendRequest<string>("editor/newFile", null);
        }

        public async Task OpenFileAsync(string filePath)
        {
            await _languageServer.SendRequest<OpenFileDetails>("editor/openFile", new OpenFileDetails
            {
                FilePath = filePath,
                Preview = DefaultPreviewSetting
            });
        }

        public async Task OpenFileAsync(string filePath, bool preview)
        {
            await _languageServer.SendRequest<OpenFileDetails>("editor/openFile", new OpenFileDetails
            {
                FilePath = filePath,
                Preview = preview
            });
        }

        public async Task CloseFileAsync(string filePath)
        {
            await _languageServer.SendRequest<string>("editor/closeFile", filePath);
        }

        public async Task SaveFileAsync(string filePath)
        {
            await SaveFileAsync(filePath, null);
        }

        public async Task SaveFileAsync(string currentPath, string newSavePath)
        {
            await _languageServer.SendRequest<SaveFileDetails>("editor/saveFile", new SaveFileDetails
            {
                FilePath = currentPath,
                NewPath = newSavePath
            });
        }

        public string GetWorkspacePath()
        {
            return _workspaceService.WorkspacePath;
        }

        public string GetWorkspaceRelativePath(string filePath)
        {
            return _workspaceService.GetRelativePath(filePath);
        }

        public async Task ShowInformationMessageAsync(string message)
        {
            await _languageServer.SendRequest<string>("editor/showInformationMessage", message);
        }

        public async Task ShowErrorMessageAsync(string message)
        {
            await _languageServer.SendRequest<string>("editor/showErrorMessage", message);
        }

        public async Task ShowWarningMessageAsync(string message)
        {
            await _languageServer.SendRequest<string>("editor/showWarningMessage", message);
        }

        public async Task SetStatusBarMessageAsync(string message, int? timeout)
        {
            await _languageServer.SendRequest<StatusBarMessageDetails>("editor/setStatusBarMessage", new StatusBarMessageDetails
            {
                Message = message,
                Timeout = timeout
            });
        }

        public void ClearTerminal()
        {
            _languageServer.SendNotification("editor/clearTerminal");
        }
    }
}
