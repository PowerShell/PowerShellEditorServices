//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class EditorOperationsService : IEditorOperations
    {
        private const bool DefaultPreviewSetting = true;

        private readonly WorkspaceService _workspaceService;
        private readonly PowerShellContextService _powerShellContextService;
        private readonly ILanguageServer _languageServer;

        public EditorOperationsService(
            WorkspaceService workspaceService,
            PowerShellContextService powerShellContextService,
            ILanguageServer languageServer)
        {
            _workspaceService = workspaceService;
            _powerShellContextService = powerShellContextService;
            _languageServer = languageServer;
        }

        public async Task<EditorContext> GetEditorContextAsync()
        {
            if (!TestHasLanguageServer())
            {
                return null;
            };

            ClientEditorContext clientContext =
                await _languageServer.SendRequest<GetEditorContextRequest>(
                    "editor/getEditorContext",
                    new GetEditorContextRequest())
                .Returning<ClientEditorContext>(CancellationToken.None)
                .ConfigureAwait(false);

            return this.ConvertClientEditorContext(clientContext);
        }

        public async Task InsertTextAsync(string filePath, string text, BufferRange insertRange)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/insertText", new InsertTextRequest
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
            }).ReturningVoid(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task SetSelectionAsync(BufferRange selectionRange)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/setSelection", new SetSelectionRequest
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
            }).ReturningVoid(CancellationToken.None).ConfigureAwait(false);
        }

        public EditorContext ConvertClientEditorContext(
            ClientEditorContext clientContext)
        {
            ScriptFile scriptFile = _workspaceService.GetFileBuffer(
                clientContext.CurrentFilePath,
                clientContext.CurrentFileContent);

            return
                new EditorContext(
                    this,
                    scriptFile,
                    new BufferPosition(
                        clientContext.CursorPosition.Line + 1,
                        clientContext.CursorPosition.Character + 1),
                    new BufferRange(
                        clientContext.SelectionRange.Start.Line + 1,
                        clientContext.SelectionRange.Start.Character + 1,
                        clientContext.SelectionRange.End.Line + 1,
                        clientContext.SelectionRange.End.Character + 1),
                    clientContext.CurrentFileLanguage);
        }

        public async Task NewFileAsync()
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<string>("editor/newFile", null)
                .ReturningVoid(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task OpenFileAsync(string filePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/openFile", new OpenFileDetails
            {
                FilePath = filePath,
                Preview = DefaultPreviewSetting
            }).ReturningVoid(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task OpenFileAsync(string filePath, bool preview)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/openFile", new OpenFileDetails
            {
                FilePath = filePath,
                Preview = preview
            }).ReturningVoid(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task CloseFileAsync(string filePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/closeFile", filePath)
                .ReturningVoid(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public Task SaveFileAsync(string filePath)
        {
            return SaveFileAsync(filePath, null);
        }

        public async Task SaveFileAsync(string currentPath, string newSavePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/saveFile", new SaveFileDetails
            {
                FilePath = currentPath,
                NewPath = newSavePath
            }).ReturningVoid(CancellationToken.None).ConfigureAwait(false);
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
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/showInformationMessage", message)
                .ReturningVoid(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task ShowErrorMessageAsync(string message)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/showErrorMessage", message)
                .ReturningVoid(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task ShowWarningMessageAsync(string message)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest("editor/showWarningMessage", message)
                .ReturningVoid(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task SetStatusBarMessageAsync(string message, int? timeout)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/setStatusBarMessage", new StatusBarMessageDetails
            {
                Message = message,
                Timeout = timeout
            }).ReturningVoid(CancellationToken.None).ConfigureAwait(false);
        }

        public void ClearTerminal()
        {
            if (!TestHasLanguageServer(warnUser: false))
            {
                return;
            };

            _languageServer.SendNotification("editor/clearTerminal");
        }

        private bool TestHasLanguageServer(bool warnUser = true)
        {
            if (_languageServer != null)
            {
                return true;
            }

            if (warnUser)
            {
                _powerShellContextService.ExternalHost.UI.WriteWarningLine(
                    "Editor operations are not supported in temporary consoles. Re-run the command in the main PowerShell Intergrated Console.");
            }

            return false;
        }
    }
}
