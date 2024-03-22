﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.Extension
{
    internal class EditorOperationsService : IEditorOperations
    {
        private readonly PsesInternalHost _psesHost;
        private readonly WorkspaceService _workspaceService;
        private readonly ILanguageServerFacade _languageServer;

        public EditorOperationsService(
            PsesInternalHost psesHost,
            WorkspaceService workspaceService,
            ILanguageServerFacade languageServer)
        {
            _psesHost = psesHost;
            _workspaceService = workspaceService;
            _languageServer = languageServer;
        }

        public async Task<EditorContext> GetEditorContextAsync()
        {
            if (!TestHasLanguageServer())
            {
                return null;
            }

            ClientEditorContext clientContext =
                await _languageServer.SendRequest(
                    "editor/getEditorContext",
                    new GetEditorContextRequest())
                .Returning<ClientEditorContext>(CancellationToken.None)
                .ConfigureAwait(false);

            return ConvertClientEditorContext(clientContext);
        }

        public async Task InsertTextAsync(string filePath, string text, BufferRange insertRange)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

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
            }).Returning<EditorOperationResponse>(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task SetSelectionAsync(BufferRange selectionRange)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

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
            }).Returning<EditorOperationResponse>(CancellationToken.None).ConfigureAwait(false);
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

        public async Task NewFileAsync() => await NewFileAsync(string.Empty).ConfigureAwait(false);

        public async Task NewFileAsync(string content)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/newFile", content)
                .Returning<EditorOperationResponse>(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task OpenFileAsync(string filePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/openFile", new OpenFileDetails
            {
                FilePath = filePath,
                Preview = true
            }).Returning<EditorOperationResponse>(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task OpenFileAsync(string filePath, bool preview)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/openFile", new OpenFileDetails
            {
                FilePath = filePath,
                Preview = preview
            }).Returning<EditorOperationResponse>(CancellationToken.None).ConfigureAwait(false);
        }

        public async Task CloseFileAsync(string filePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/closeFile", filePath)
                .Returning<EditorOperationResponse>(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public Task SaveFileAsync(string filePath) => SaveFileAsync(filePath, null);

        public async Task SaveFileAsync(string currentPath, string newSavePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/saveFile", new SaveFileDetails
            {
                FilePath = currentPath,
                NewPath = newSavePath
            }).Returning<EditorOperationResponse>(CancellationToken.None).ConfigureAwait(false);
        }

        // NOTE: This name is now outdated since we don't have a way to distinguish one workspace
        // from another for the extension API. TODO: Should this be an empty string if we have no
        // workspaces?
        public string GetWorkspacePath() => _workspaceService.InitialWorkingDirectory;

        public string[] GetWorkspacePaths() => _workspaceService.WorkspacePaths.ToArray();

        public string GetWorkspaceRelativePath(ScriptFile scriptFile) => _workspaceService.GetRelativePath(scriptFile);

        public async Task ShowInformationMessageAsync(string message)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/showInformationMessage", message)
                .Returning<EditorOperationResponse>(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task ShowErrorMessageAsync(string message)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/showErrorMessage", message)
                .Returning<EditorOperationResponse>(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task ShowWarningMessageAsync(string message)
        {
            if (!TestHasLanguageServer())
            {
                return;
            }

            await _languageServer.SendRequest("editor/showWarningMessage", message)
                .Returning<EditorOperationResponse>(CancellationToken.None)
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
            }).Returning<EditorOperationResponse>(CancellationToken.None).ConfigureAwait(false);
        }

        public void ClearTerminal()
        {
            if (!TestHasLanguageServer(warnUser: false))
            {
                return;
            }

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
                _psesHost.UI.WriteWarningLine(
                    "Editor operations are not supported in temporary consoles. Re-run the command in the main Extension Terminal.");
            }

            return false;
        }
    }
}
