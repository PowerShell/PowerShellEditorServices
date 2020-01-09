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
        private PowerShellContextService _powerShellContextService;
        private ILanguageServer _languageServer;

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
                await _languageServer.SendRequest<GetEditorContextRequest, ClientEditorContext>(
                    "editor/getEditorContext",
                    new GetEditorContextRequest());

            return this.ConvertClientEditorContext(clientContext);
        }

        public async Task InsertTextAsync(string filePath, string text, BufferRange insertRange)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

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
            if (!TestHasLanguageServer())
            {
                return;
            };

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
            ScriptFile scriptFile = _workspaceService.GetFileBuffer(
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
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<string>("editor/newFile", null);
        }

        public async Task OpenFileAsync(string filePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<OpenFileDetails>("editor/openFile", new OpenFileDetails
            {
                FilePath = filePath,
                Preview = DefaultPreviewSetting
            });
        }

        public async Task OpenFileAsync(string filePath, bool preview)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<OpenFileDetails>("editor/openFile", new OpenFileDetails
            {
                FilePath = filePath,
                Preview = preview
            });
        }

        public async Task CloseFileAsync(string filePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<string>("editor/closeFile", filePath);
        }

        public async Task SaveFileAsync(string filePath)
        {
            await SaveFileAsync(filePath, null);
        }

        public async Task SaveFileAsync(string currentPath, string newSavePath)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

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
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<string>("editor/showInformationMessage", message);
        }

        public async Task ShowErrorMessageAsync(string message)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<string>("editor/showErrorMessage", message);
        }

        public async Task ShowWarningMessageAsync(string message)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<string>("editor/showWarningMessage", message);
        }

        public async Task SetStatusBarMessageAsync(string message, int? timeout)
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            await _languageServer.SendRequest<StatusBarMessageDetails>("editor/setStatusBarMessage", new StatusBarMessageDetails
            {
                Message = message,
                Timeout = timeout
            });
        }

        public void ClearTerminal()
        {
            if (!TestHasLanguageServer())
            {
                return;
            };

            _languageServer.SendNotification("editor/clearTerminal");
        }

        private bool TestHasLanguageServer()
        {
            if (_languageServer != null)
            {
                return true;
            }

            _powerShellContextService.ExternalHost.UI.WriteWarningLine(
                "Editor operations are not supported in temporary consoles. Re-run the command in the main PowerShell Intergrated Console.");
            return false;
        }
    }
}
