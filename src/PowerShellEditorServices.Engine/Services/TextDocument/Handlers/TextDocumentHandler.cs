using System;
using System.Collections.Generic;
using System.Text;
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

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    class TextDocumentHandler : ITextDocumentSyncHandler
    {

        private readonly ILogger _logger;
        private readonly ILanguageServer _languageServer;
        private readonly AnalysisService _analysisService;
        private readonly WorkspaceService _workspaceService;

        private Dictionary<string, Dictionary<string, MarkerCorrection>> codeActionsPerFile =
            new Dictionary<string, Dictionary<string, MarkerCorrection>>();

        private static CancellationTokenSource s_existingRequestCancellation;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.ps*1"
            }
        );

        private SynchronizationCapability _capability;

        public TextDocumentSyncKind Change => TextDocumentSyncKind.Incremental;

        public TextDocumentHandler(ILoggerFactory factory, ILanguageServer languageServer, AnalysisService analysisService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<TextDocumentHandler>();
            _languageServer = languageServer;
            _analysisService = analysisService;
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
            RunScriptDiagnosticsAsync(changedFiles.ToArray());
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
            RunScriptDiagnosticsAsync(new ScriptFile[] { openedFile });

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
                ClearMarkers(fileToClose);
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

        private Task RunScriptDiagnosticsAsync(
            ScriptFile[] filesToAnalyze)
        {
            // If there's an existing task, attempt to cancel it
            try
            {
                if (s_existingRequestCancellation != null)
                {
                    // Try to cancel the request
                    s_existingRequestCancellation.Cancel();

                    // If cancellation didn't throw an exception,
                    // clean up the existing token
                    s_existingRequestCancellation.Dispose();
                    s_existingRequestCancellation = null;
                }
            }
            catch (Exception e)
            {
                // TODO: Catch a more specific exception!
                _logger.LogError(
                    string.Format(
                        "Exception while canceling analysis task:\n\n{0}",
                        e.ToString()));

                TaskCompletionSource<bool> cancelTask = new TaskCompletionSource<bool>();
                cancelTask.SetCanceled();
                return cancelTask.Task;
            }

            // If filesToAnalzye is empty, nothing to do so return early.
            if (filesToAnalyze.Length == 0)
            {
                return Task.FromResult(true);
            }

            // Create a fresh cancellation token and then start the task.
            // We create this on a different TaskScheduler so that we
            // don't block the main message loop thread.
            // TODO: Is there a better way to do this?
            s_existingRequestCancellation = new CancellationTokenSource();
            // TODO use settings service
            Task.Factory.StartNew(
                () =>
                    DelayThenInvokeDiagnosticsAsync(
                        750,
                        filesToAnalyze,
                        true,
                        this.codeActionsPerFile,
                        _logger,
                        s_existingRequestCancellation.Token),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);

            return Task.FromResult(true);
        }

        private async Task DelayThenInvokeDiagnosticsAsync(
            int delayMilliseconds,
            ScriptFile[] filesToAnalyze,
            bool isScriptAnalysisEnabled,
            Dictionary<string, Dictionary<string, MarkerCorrection>> correctionIndex,
            ILogger Logger,
            CancellationToken cancellationToken)
        {
            // First of all, wait for the desired delay period before
            // analyzing the provided list of files
            try
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // If the task is cancelled, exit directly
                foreach (var script in filesToAnalyze)
                {
                    PublishScriptDiagnostics(
                        script,
                        script.DiagnosticMarkers,
                        correctionIndex);
                }

                return;
            }

            // If we've made it past the delay period then we don't care
            // about the cancellation token anymore.  This could happen
            // when the user stops typing for long enough that the delay
            // period ends but then starts typing while analysis is going
            // on.  It makes sense to send back the results from the first
            // delay period while the second one is ticking away.

            // Get the requested files
            foreach (ScriptFile scriptFile in filesToAnalyze)
            {
                List<ScriptFileMarker> semanticMarkers = null;
                if (isScriptAnalysisEnabled && _analysisService != null)
                {
                    semanticMarkers = await _analysisService.GetSemanticMarkersAsync(scriptFile);
                }
                else
                {
                    // Semantic markers aren't available if the AnalysisService
                    // isn't available
                    semanticMarkers = new List<ScriptFileMarker>();
                }

                scriptFile.DiagnosticMarkers.AddRange(semanticMarkers);

                PublishScriptDiagnostics(
                    scriptFile,
                    // Concat script analysis errors to any existing parse errors
                    scriptFile.DiagnosticMarkers,
                    correctionIndex);
            }
        }

        private void ClearMarkers(ScriptFile scriptFile)
        {
            // send empty diagnostic markers to clear any markers associated with the given file
            PublishScriptDiagnostics(
                    scriptFile,
                    new List<ScriptFileMarker>(),
                    this.codeActionsPerFile);
        }

        private void PublishScriptDiagnostics(
            ScriptFile scriptFile,
            List<ScriptFileMarker> markers,
            Dictionary<string, Dictionary<string, MarkerCorrection>> correctionIndex)
        {
            List<Diagnostic> diagnostics = new List<Diagnostic>();

            // Hold on to any corrections that may need to be applied later
            Dictionary<string, MarkerCorrection> fileCorrections =
                new Dictionary<string, MarkerCorrection>();

            foreach (var marker in markers)
            {
                // Does the marker contain a correction?
                Diagnostic markerDiagnostic = GetDiagnosticFromMarker(marker);
                if (marker.Correction != null)
                {
                    string diagnosticId = GetUniqueIdFromDiagnostic(markerDiagnostic);
                    fileCorrections.Add(diagnosticId, marker.Correction);
                }

                diagnostics.Add(markerDiagnostic);
            }

            correctionIndex[scriptFile.DocumentUri] = fileCorrections;

            var uriBuilder = new UriBuilder()
            {
                Scheme = Uri.UriSchemeFile,
                Path = scriptFile.FilePath,
                Host = string.Empty,
            };

            // Always send syntax and semantic errors.  We want to
            // make sure no out-of-date markers are being displayed.
            _languageServer.Document.PublishDiagnostics(new PublishDiagnosticsParams()
            {
                Uri = uriBuilder.Uri,
                Diagnostics = new Container<Diagnostic>(diagnostics),
            });
        }

        // Generate a unique id that is used as a key to look up the associated code action (code fix) when
        // we receive and process the textDocument/codeAction message.
        private static string GetUniqueIdFromDiagnostic(Diagnostic diagnostic)
        {
            Position start = diagnostic.Range.Start;
            Position end = diagnostic.Range.End;

            var sb = new StringBuilder(256)
            .Append(diagnostic.Source ?? "?")
            .Append("_")
            .Append(diagnostic.Code.ToString())
            .Append("_")
            .Append(diagnostic.Severity?.ToString() ?? "?")
            .Append("_")
            .Append(start.Line)
            .Append(":")
            .Append(start.Character)
            .Append("-")
            .Append(end.Line)
            .Append(":")
            .Append(end.Character);

            var id = sb.ToString();
            return id;
        }

        private static Diagnostic GetDiagnosticFromMarker(ScriptFileMarker scriptFileMarker)
        {
            return new Diagnostic
            {
                Severity = MapDiagnosticSeverity(scriptFileMarker.Level),
                Message = scriptFileMarker.Message,
                Code = scriptFileMarker.RuleName,
                Source = scriptFileMarker.Source,
                Range = new Range
                {
                    Start = new Position
                    {
                        Line = scriptFileMarker.ScriptRegion.StartLineNumber - 1,
                        Character = scriptFileMarker.ScriptRegion.StartColumnNumber - 1
                    },
                    End = new Position
                    {
                        Line = scriptFileMarker.ScriptRegion.EndLineNumber - 1,
                        Character = scriptFileMarker.ScriptRegion.EndColumnNumber - 1
                    }
                }
            };
        }

        private static DiagnosticSeverity MapDiagnosticSeverity(ScriptFileMarkerLevel markerLevel)
        {
            switch (markerLevel)
            {
                case ScriptFileMarkerLevel.Error:
                    return DiagnosticSeverity.Error;

                case ScriptFileMarkerLevel.Warning:
                    return DiagnosticSeverity.Warning;

                case ScriptFileMarkerLevel.Information:
                    return DiagnosticSeverity.Information;

                default:
                    return DiagnosticSeverity.Error;
            }
        }
    }
}
