using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;
using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace PowerShellEditorServices.Engine.Services.Workspace.Handlers
{
    class TextDocumentHandler : ITextDocumentSyncHandler
    {

        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.ps*1"
            }
        );

        private SynchronizationCapability _capability;

        public TextDocumentSyncKind Change => TextDocumentSyncKind.Incremental;

        public TextDocumentHandler(ILoggerFactory factory, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<TextDocumentHandler>();
            _workspaceService = workspaceService;
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
        {
            List<ScriptFile> changedFiles = new List<ScriptFile>();

            // A text change notification can batch multiple change requests
            foreach (TextDocumentContentChangeEvent textChange in notification.ContentChanges)
            {
                ScriptFile changedFile = _workspaceService.GetFile(notification.TextDocument.Uri.ToString());

                changedFile.ApplyChange(
                    GetFileChangeDetails(
                        textChange.Range,
                        textChange.Text));

                changedFiles.Add(changedFile);
            }

            // TODO: Get all recently edited files in the workspace
            // this.RunScriptDiagnosticsAsync(
            //     changedFiles.ToArray(),
            //     editorSession,
            //     eventContext);
            return Unit.Task;
        }

        TextDocumentChangeRegistrationOptions IRegistration<TextDocumentChangeRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = Change
            };
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            _capability = capability;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
        {
            ScriptFile openedFile =
                _workspaceService.GetFileBuffer(
                    notification.TextDocument.Uri.ToString(),
                    notification.TextDocument.Text);

            // TODO: Get all recently edited files in the workspace
            // this.RunScriptDiagnosticsAsync(
            //     new ScriptFile[] { openedFile },
            //     editorSession,
            //     eventContext);

            _logger.LogTrace("Finished opening document.");
            return Unit.Task;
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
        {
            // Find and close the file in the current session
            var fileToClose = _workspaceService.GetFile(notification.TextDocument.Uri.ToString());

            if (fileToClose != null)
            {
                _workspaceService.CloseFile(fileToClose);
                // await ClearMarkersAsync(fileToClose, eventContext);
            }

            _logger.LogTrace("Finished closing document.");
            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token)
        {
            ScriptFile savedFile =
                _workspaceService.GetFile(
                    notification.TextDocument.Uri.ToString());

            // if (savedFile != null)
            // {
            //     if (this.editorSession.RemoteFileManager.IsUnderRemoteTempPath(savedFile.FilePath))
            //     {
            //         await this.editorSession.RemoteFileManager.SaveRemoteFileAsync(
            //             savedFile.FilePath);
            //     }
            // }
            return Unit.Task;
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                IncludeText = true
            };
        }
        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            return new TextDocumentAttributes(uri, "powershell");
        }

        private static FileChange GetFileChangeDetails(Range changeRange, string insertString)
        {
            // The protocol's positions are zero-based so add 1 to all offsets

            if (changeRange == null) return new FileChange { InsertString = insertString, IsReload = true };

            return new FileChange
            {
                InsertString = insertString,
                Line = (int)(changeRange.Start.Line + 1),
                Offset = (int)(changeRange.Start.Character + 1),
                EndLine = (int)(changeRange.End.Line + 1),
                EndOffset = (int)(changeRange.End.Character + 1),
                IsReload = false
            };
        }

        // private async Task ClearMarkersAsync(ScriptFile scriptFile, EventContext eventContext)
        // {
        //     // send empty diagnostic markers to clear any markers associated with the given file
        //     await PublishScriptDiagnosticsAsync(
        //             scriptFile,
        //             new List<ScriptFileMarker>(),
        //             this.codeActionsPerFile,
        //             eventContext);
        // }
    }
}
