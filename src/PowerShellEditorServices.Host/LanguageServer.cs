//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using DebugAdapterMessages = Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Microsoft.PowerShell.EditorServices.Host
{
    internal class LanguageServer : IMessageProcessor
    {
        private static CancellationTokenSource existingRequestCancellation;

        private MessageDispatcher<EditorSession> messageDispatcher;

        public LanguageServer()
        {
            this.messageDispatcher = new MessageDispatcher<EditorSession>();
        }

        public void Initialize()
        {
            // Register all supported message types

            this.AddRequestHandler(InitializeRequest.Type, this.HandleInitializeRequest);
            this.AddRequestHandler(ShutdownRequest.Type, this.HandleShutdownRequest);
            this.AddEventHandler(ExitNotification.Type, this.HandleExitNotification);

            this.AddEventHandler(DidOpenTextDocumentNotification.Type, this.HandleDidOpenTextDocumentNotification);
            this.AddEventHandler(DidCloseTextDocumentNotification.Type, this.HandleDidCloseTextDocumentNotification);
            this.AddEventHandler(DidChangeTextDocumentNotification.Type, this.HandleDidChangeTextDocumentNotification);

            this.AddRequestHandler(DefinitionRequest.Type, this.HandleDefinitionRequest);
            this.AddRequestHandler(ReferencesRequest.Type, this.HandleReferencesRequest);
            this.AddRequestHandler(CompletionRequest.Type, this.HandleCompletionRequest);
            this.AddRequestHandler(CompletionResolveRequest.Type, this.HandleCompletionResolveRequest);
            this.AddRequestHandler(SignatureHelpRequest.Type, this.HandleSignatureHelpRequest);
            this.AddRequestHandler(DocumentHighlightRequest.Type, this.HandleDocumentHighlightRequest);
            this.AddRequestHandler(HoverRequest.Type, this.HandleHoverRequest);
            this.AddRequestHandler(DocumentSymbolRequest.Type, this.HandleDocumentSymbolRequest);
            this.AddRequestHandler(WorkspaceSymbolRequest.Type, this.HandleWorkspaceSymbolRequest);

            this.AddRequestHandler(ShowOnlineHelpRequest.Type, this.HandleShowOnlineHelpRequest);

            this.AddRequestHandler(DebugAdapterMessages.EvaluateRequest.Type, this.HandleEvaluateRequest);
        }

        public void AddRequestHandler<TParams, TResult, TError>(
            RequestType<TParams, TResult, TError> requestType,
            Func<TParams, EditorSession, RequestContext<TResult, TError>, Task> requestHandler)
        {
            this.messageDispatcher.AddRequestHandler(
                requestType,
                requestHandler);
        }

        public void AddEventHandler<TParams>(
            EventType<TParams> eventType,
            Func<TParams, EditorSession, EventContext, Task> eventHandler)
        {
            this.messageDispatcher.AddEventHandler(
                eventType,
                eventHandler);
        }

        public async Task ProcessMessage(
            Message messageToProcess,
            EditorSession editorSession,
            MessageWriter messageWriter)
        {
            await this.messageDispatcher.DispatchMessage(
                messageToProcess,
                editorSession,
                messageWriter);
        }

        #region Built-in Message Handlers

        protected async Task HandleInitializeRequest(
            InitializeRequest initializeParams,
            EditorSession editorSession,
            RequestContext<InitializeResult, InitializeError> requestContext)
        {
            await requestContext.SendResult(
                new InitializeResult
                {
                    Capabilities = new ServerCapabilities
                    {
                        TextDocumentSync = TextDocumentSyncKind.Incremental,
                        DefinitionProvider = true,
                        ReferencesProvider = true,
                        DocumentHighlightProvider = true,
                        DocumentSymbolProvider = true,
                        WorkspaceSymbolProvider = true,
                        HoverProvider = true,
                        CompletionProvider = new CompletionOptions
                        {
                            ResolveProvider = true,
                            TriggerCharacters = new string[] { ".", "-", ":", "\\" }
                        },
                        SignatureHelpProvider = new SignatureHelpOptions
                        {
                            TriggerCharacters = new string[] { " " } // TODO: Other characters here?
                        }
                    }
                });
        }

        protected Task HandleShutdownRequest(
            object shutdownParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            // TODO: Shut down!

            return Task.FromResult(true);
        }

        protected async Task HandleShowOnlineHelpRequest(
            object helpParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            var psCommand = new PSCommand();

            if (helpParams == null) { helpParams = "get-help"; }

            var script = string.Format("get-help {0} -Online", helpParams);

            psCommand.AddScript(script);

            var result = await editorSession.powerShellContext.ExecuteCommand<object>(
                        psCommand);

            await requestContext.SendResult(null);
        }

        protected Task HandleExitNotification(
            object exitParams,
            EditorSession editorSession,
            EventContext eventContext)
        {
            // TODO: Shut down!

            return Task.FromResult(true);
        }

        protected Task HandleDidOpenTextDocumentNotification(
            DidOpenTextDocumentNotification openParams,
            EditorSession editorSession,
            EventContext eventContext)
        {
            ScriptFile openedFile =
                editorSession.Workspace.GetFileBuffer(
                    openParams.Uri,
                    openParams.Text);

            // TODO: Get all recently edited files in the workspace
            this.RunScriptDiagnostics(
                new ScriptFile[] { openedFile },
                editorSession,
                eventContext);

            Logger.Write(LogLevel.Verbose, "Finished opening document.");

            return Task.FromResult(true);
        }

        protected Task HandleDidCloseTextDocumentNotification(
            TextDocumentIdentifier closeParams,
            EditorSession editorSession,
            EventContext eventContext)
        {
            // Find and close the file in the current session
            var fileToClose = editorSession.Workspace.GetFile(closeParams.Uri);

            if (fileToClose != null)
            {
                editorSession.Workspace.CloseFile(fileToClose);
            }

            Logger.Write(LogLevel.Verbose, "Finished closing document.");

            return Task.FromResult(true);
        }

        protected Task HandleDidChangeTextDocumentNotification(
            DidChangeTextDocumentParams textChangeParams,
            EditorSession editorSession,
            EventContext eventContext)
        {
            List<ScriptFile> changedFiles = new List<ScriptFile>();

            // A text change notification can batch multiple change requests
            foreach (var textChange in textChangeParams.ContentChanges)
            {
                ScriptFile changedFile = editorSession.Workspace.GetFile(textChangeParams.Uri);

                changedFile.ApplyChange(
                    GetFileChangeDetails(
                        textChange.Range.Value,
                        textChange.Text));

                changedFiles.Add(changedFile);
            }

            // TODO: Get all recently edited files in the workspace
            this.RunScriptDiagnostics(
                changedFiles.ToArray(),
                editorSession,
                eventContext);

            return Task.FromResult(true);
        }

        protected async Task HandleDefinitionRequest(
            TextDocumentPosition textDocumentPosition,
            EditorSession editorSession,
            RequestContext<Location[], object> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPosition.Uri);

            SymbolReference foundSymbol =
                editorSession.LanguageService.FindSymbolAtLocation(
                    scriptFile,
                    textDocumentPosition.Position.Line + 1,
                    textDocumentPosition.Position.Character + 1);

            List<Location> definitionLocations = new List<Location>();

            GetDefinitionResult definition = null;
            if (foundSymbol != null)
            {
                definition =
                    await editorSession.LanguageService.GetDefinitionOfSymbol(
                        scriptFile,
                        foundSymbol,
                        editorSession.Workspace);

                if (definition != null)
                {
                    definitionLocations.Add(
                        new Location
                        {
                            Uri = new Uri(definition.FoundDefinition.FilePath).AbsoluteUri,
                            Range = GetRangeFromScriptRegion(definition.FoundDefinition.ScriptRegion)
                        });
                }
            }

            await requestContext.SendResult(definitionLocations.ToArray());
        }

        protected async Task HandleReferencesRequest(
            ReferencesParams referencesParams,
            EditorSession editorSession,
            RequestContext<Location[], object> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    referencesParams.Uri);

            SymbolReference foundSymbol =
                editorSession.LanguageService.FindSymbolAtLocation(
                    scriptFile,
                    referencesParams.Position.Line + 1,
                    referencesParams.Position.Character + 1);

            FindReferencesResult referencesResult =
                await editorSession.LanguageService.FindReferencesOfSymbol(
                    foundSymbol,
                    editorSession.Workspace.ExpandScriptReferences(scriptFile));

            Location[] referenceLocations = null;

            if (referencesResult != null)
            {
                referenceLocations =
                    referencesResult
                        .FoundReferences
                        .Select(r =>
                            {
                                return new Location
                                {
                                    Uri = new Uri(r.FilePath).AbsoluteUri,
                                    Range = GetRangeFromScriptRegion(r.ScriptRegion)
                                };
                            })
                        .ToArray();
            }
            else
            {
                referenceLocations = new Location[0];
            }

            await requestContext.SendResult(referenceLocations);
        }

        protected async Task HandleCompletionRequest(
            TextDocumentPosition textDocumentPosition,
            EditorSession editorSession,
            RequestContext<CompletionItem[], object> requestContext)
        {
            int cursorLine = textDocumentPosition.Position.Line + 1;
            int cursorColumn = textDocumentPosition.Position.Character + 1;

            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPosition.Uri);

            CompletionResults completionResults =
                await editorSession.LanguageService.GetCompletionsInFile(
                    scriptFile,
                    cursorLine,
                    cursorColumn);

            CompletionItem[] completionItems = null;

            if (completionResults != null)
            {
                // By default, insert the completion at the current location
                int startEditColumn = textDocumentPosition.Position.Character;
                int endEditColumn = textDocumentPosition.Position.Character;

                // Find the extents of the token under the cursor
                var completedToken =
                    scriptFile
                        .ScriptAst
                        .FindAll(
                            ast =>
                            {
                                return
                                    !(ast is PipelineAst) &&
                                    ast.Extent.StartLineNumber == cursorLine &&
                                    ast.Extent.StartColumnNumber <= cursorColumn &&
                                    ast.Extent.EndColumnNumber >= cursorColumn;
                            },
                            true)
                        .LastOrDefault();   // The most relevant AST will be the last

                if (completedToken != null)
                {
                    // The edit should replace the token that was found at the cursor position
                    startEditColumn = completedToken.Extent.StartColumnNumber - 1;
                    endEditColumn = completedToken.Extent.EndColumnNumber - 1;
                }

                completionItems =
                    completionResults
                        .Completions
                        .Select(
                            c => CreateCompletionItem(
                                c,
                                textDocumentPosition.Position.Line,
                                startEditColumn,
                                endEditColumn))
                        .ToArray();
            }
            else
            {
                completionItems = new CompletionItem[0];
            }

            await requestContext.SendResult(completionItems);
        }

        protected async Task HandleCompletionResolveRequest(
            CompletionItem completionItem,
            EditorSession editorSession,
            RequestContext<CompletionItem, object> requestContext)
        {
            if (completionItem.Kind == CompletionItemKind.Function)
            {
                RunspaceHandle runspaceHandle =
                    await editorSession.powerShellContext.GetRunspaceHandle();

                // Get the documentation for the function
                CommandInfo commandInfo =
                    CommandHelpers.GetCommandInfo(
                        completionItem.Label,
                        runspaceHandle.Runspace);

                if (commandInfo != null)
                {
                    completionItem.Documentation =
                        CommandHelpers.GetCommandSynopsis(
                            commandInfo,
                            runspaceHandle.Runspace);
                }

                runspaceHandle.Dispose();
            }

            // Send back the updated CompletionItem
            await requestContext.SendResult(completionItem);
        }

        protected async Task HandleSignatureHelpRequest(
            TextDocumentPosition textDocumentPosition,
            EditorSession editorSession,
            RequestContext<SignatureHelp, object> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPosition.Uri);

            ParameterSetSignatures parameterSets =
                await editorSession.LanguageService.FindParameterSetsInFile(
                    scriptFile,
                    textDocumentPosition.Position.Line + 1,
                    textDocumentPosition.Position.Character + 1);

            SignatureInformation[] signatures = null;
            int? activeParameter = null;
            int? activeSignature = 0;

            if (parameterSets != null)
            {
                signatures =
                    parameterSets
                        .Signatures
                        .Select(s =>
                            {
                                return new SignatureInformation
                                {
                                    Label = parameterSets.CommandName + " " + s.SignatureText,
                                    Documentation = null,
                                    Parameters =
                                        s.Parameters
                                            .Select(CreateParameterInfo)
                                            .ToArray()
                                };
                            })
                        .ToArray();
            }
            else
            {
                signatures = new SignatureInformation[0];
            }

            await requestContext.SendResult(
                new SignatureHelp
                {
                    Signatures = signatures,
                    ActiveParameter = activeParameter,
                    ActiveSignature = activeSignature
                });
        }

        protected async Task HandleDocumentHighlightRequest(
            TextDocumentPosition textDocumentPosition,
            EditorSession editorSession,
            RequestContext<DocumentHighlight[], object> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPosition.Uri);

            FindOccurrencesResult occurrencesResult =
                editorSession.LanguageService.FindOccurrencesInFile(
                    scriptFile,
                    textDocumentPosition.Position.Line + 1,
                    textDocumentPosition.Position.Character + 1);

            DocumentHighlight[] documentHighlights = null;

            if (occurrencesResult != null)
            {
                documentHighlights =
                    occurrencesResult
                        .FoundOccurrences
                        .Select(o =>
                            {
                                return new DocumentHighlight
                                {
                                    Kind = DocumentHighlightKind.Write, // TODO: Which symbol types are writable?
                                    Range = GetRangeFromScriptRegion(o.ScriptRegion)
                                };
                            })
                        .ToArray();
            }
            else
            {
                documentHighlights = new DocumentHighlight[0];
            }

            await requestContext.SendResult(documentHighlights);
        }

        protected async Task HandleHoverRequest(
            TextDocumentPosition textDocumentPosition,
            EditorSession editorSession,
            RequestContext<Hover, object> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPosition.Uri);

            SymbolDetails symbolDetails =
                await editorSession
                    .LanguageService
                    .FindSymbolDetailsAtLocation(
                        scriptFile,
                        textDocumentPosition.Position.Line + 1,
                        textDocumentPosition.Position.Character + 1);

            List<MarkedString> symbolInfo = new List<MarkedString>();
            Range? symbolRange = null;

            if (symbolDetails != null)
            {
                symbolInfo.Add(
                    new MarkedString
                    {
                        Language = "PowerShell",
                        Value = symbolDetails.DisplayString
                    });

                if (!string.IsNullOrEmpty(symbolDetails.Documentation))
                {
                    symbolInfo.Add(
                        new MarkedString
                        {
                            Language = "markdown",
                            Value = symbolDetails.Documentation
                        });
                }

                symbolRange = GetRangeFromScriptRegion(symbolDetails.SymbolReference.ScriptRegion);
            }

            await requestContext.SendResult(
                new Hover
                {
                    Contents = symbolInfo.ToArray(),
                    Range = symbolRange
                });
        }

        protected async Task HandleDocumentSymbolRequest(
            TextDocumentIdentifier textDocumentIdentifier,
            EditorSession editorSession,
            RequestContext<SymbolInformation[], object> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentIdentifier.Uri);

            FindOccurrencesResult foundSymbols =
                editorSession.LanguageService.FindSymbolsInFile(
                    scriptFile);

            SymbolInformation[] symbols = null;

            string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

            if (foundSymbols != null)
            {
                symbols =
                    foundSymbols
                        .FoundOccurrences
                        .Select(r =>
                            {
                                return new SymbolInformation
                                {
                                    ContainerName = containerName,
                                    Kind = GetSymbolKind(r.SymbolType),
                                    Location = new Location
                                    {
                                        Uri = new Uri(r.FilePath).AbsolutePath,
                                        Range = GetRangeFromScriptRegion(r.ScriptRegion)
                                    },
                                    Name = GetDecoratedSymbolName(r)
                                };
                            })
                        .ToArray();
            }
            else
            {
                symbols = new SymbolInformation[0];
            }

            await requestContext.SendResult(symbols);
        }

        private SymbolKind GetSymbolKind(SymbolType symbolType)
        {
            switch (symbolType)
            {
                case SymbolType.Configuration:
                case SymbolType.Function:
                case SymbolType.Workflow:
                    return SymbolKind.Function;

                default:
                    return SymbolKind.Variable;
            }
        }

        private string GetDecoratedSymbolName(SymbolReference symbolReference)
        {
            string name = symbolReference.SymbolName;

            if (symbolReference.SymbolType == SymbolType.Configuration ||
                symbolReference.SymbolType == SymbolType.Function ||
                symbolReference.SymbolType == SymbolType.Workflow)
            {
                name += " { }";
            }

            return name;
        }

        protected async Task HandleWorkspaceSymbolRequest(
            WorkspaceSymbolParams workspaceSymbolParams,
            EditorSession editorSession,
            RequestContext<SymbolInformation[], object> requestContext)
        {
            var symbols = new List<SymbolInformation>();

            foreach (ScriptFile scriptFile in editorSession.Workspace.GetOpenedFiles())
            {
                FindOccurrencesResult foundSymbols =
                    editorSession.LanguageService.FindSymbolsInFile(
                        scriptFile);

                // TODO: Need to compute a relative path that is based on common path for all workspace files
                string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

                if (foundSymbols != null)
                {
                    var matchedSymbols =
                        foundSymbols
                            .FoundOccurrences
                            .Where(r => IsQueryMatch(workspaceSymbolParams.Query, r.SymbolName))
                            .Select(r =>
                                {
                                    return new SymbolInformation
                                    {
                                        ContainerName = containerName,
                                        Kind = r.SymbolType == SymbolType.Variable ? SymbolKind.Variable : SymbolKind.Function,
                                        Location = new Location
                                        {
                                            Uri = new Uri(r.FilePath).AbsoluteUri,
                                            Range = GetRangeFromScriptRegion(r.ScriptRegion)
                                        },
                                        Name = GetDecoratedSymbolName(r)
                                    };
                                });

                    symbols.AddRange(matchedSymbols);
                }
            }

            await requestContext.SendResult(symbols.ToArray());
        }

        private bool IsQueryMatch(string query, string symbolName)
        {
            return symbolName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected async Task HandleEvaluateRequest(
            DebugAdapterMessages.EvaluateRequestArguments evaluateParams,
            EditorSession editorSession,
            RequestContext<DebugAdapterMessages.EvaluateResponseBody, object> requestContext)
        {
            VariableDetails result =
                await editorSession.DebugService.EvaluateExpression(
                    evaluateParams.Expression,
                    evaluateParams.FrameId);

            string valueString = null;
            int variableId = 0;

            if (result != null)
            {
                valueString = result.ValueString;
                variableId =
                    result.IsExpandable ?
                        result.Id : 0;
            }

            await requestContext.SendResult(
                new DebugAdapterMessages.EvaluateResponseBody
                {
                    Result = valueString,
                    VariablesReference = variableId
                });
        }

        #endregion

        #region Helper Methods

        private static Range GetRangeFromScriptRegion(ScriptRegion scriptRegion)
        {
            return new Range
            {
                Start = new Position
                {
                    Line = scriptRegion.StartLineNumber - 1,
                    Character = scriptRegion.StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = scriptRegion.EndLineNumber - 1,
                    Character = scriptRegion.EndColumnNumber - 1
                }
            };
        }

        private static FileChange GetFileChangeDetails(Range changeRange, string insertString)
        {
            // The protocol's positions are zero-based so add 1 to all offsets

            return new FileChange
            {
                InsertString = insertString,
                Line = changeRange.Start.Line + 1,
                Offset = changeRange.Start.Character + 1,
                EndLine = changeRange.End.Line + 1,
                EndOffset = changeRange.End.Character + 1
            };
        }

        private Task RunScriptDiagnostics(
            ScriptFile[] filesToAnalyze,
            EditorSession editorSession,
            EventContext eventContext)
        {
            // If there's an existing task, attempt to cancel it
            try
            {
                if (existingRequestCancellation != null)
                {
                    // Try to cancel the request
                    existingRequestCancellation.Cancel();

                    // If cancellation didn't throw an exception,
                    // clean up the existing token
                    existingRequestCancellation.Dispose();
                    existingRequestCancellation = null;
                }
            }
            catch (Exception e)
            {
                // TODO: Catch a more specific exception!
                Logger.Write(
                    LogLevel.Error,
                    string.Format(
                        "Exception while cancelling analysis task:\n\n{0}",
                        e.ToString()));

                return TaskConstants.Canceled;
            }

            // Create a fresh cancellation token and then start the task.
            // We create this on a different TaskScheduler so that we
            // don't block the main message loop thread.
            // TODO: Is there a better way to do this?
            existingRequestCancellation = new CancellationTokenSource();
            Task.Factory.StartNew(
                () =>
                    DelayThenInvokeDiagnostics(
                        750,
                        filesToAnalyze,
                        editorSession,
                        eventContext,
                        existingRequestCancellation.Token),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);

            return TaskConstants.Completed;
        }

        private static async Task DelayThenInvokeDiagnostics(
            int delayMilliseconds,
            ScriptFile[] filesToAnalyze,
            EditorSession editorSession,
            EventContext eventContext,
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
                Logger.Write(LogLevel.Verbose, "Analyzing script file: " + scriptFile.FilePath);

                var semanticMarkers =
                    editorSession.AnalysisService.GetSemanticMarkers(
                        scriptFile);

                var allMarkers = scriptFile.SyntaxMarkers.Concat(semanticMarkers);

                // Always send syntax and semantic errors.  We want to 
                // make sure no out-of-date markers are being displayed.
                await eventContext.SendEvent(
                    PublishDiagnosticsNotification.Type,
                    new PublishDiagnosticsNotification
                    {
                        Uri = scriptFile.ClientFilePath,
                        Diagnostics =
                           allMarkers
                                .Select(GetDiagnosticFromMarker)
                                .ToArray()
                    });

            }

            Logger.Write(LogLevel.Verbose, "Analysis complete.");
        }

        private static Diagnostic GetDiagnosticFromMarker(ScriptFileMarker scriptFileMarker)
        {
            return new Diagnostic
            {
                Severity = MapDiagnosticSeverity(scriptFileMarker.Level),
                Message = scriptFileMarker.Message,
                Range = new Range
                {
                    // TODO: What offsets should I use?
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

        private static CompletionItemKind MapCompletionKind(CompletionType completionType)
        {
            switch (completionType)
            {
                case CompletionType.Command:
                    return CompletionItemKind.Function;

                case CompletionType.Method:
                    return CompletionItemKind.Method;

                case CompletionType.Variable:
                case CompletionType.ParameterName:
                    return CompletionItemKind.Variable;

                case CompletionType.Path:
                    return CompletionItemKind.File;

                default:
                    return CompletionItemKind.Text;
            }
        }

        private static CompletionItem CreateCompletionItem(
            CompletionDetails completionDetails,
            int lineNumber,
            int startColumn,
            int endColumn)
        {
            string detailString = null;

            if (completionDetails.CompletionType == CompletionType.Variable)
            {
                // Look for variable type encoded in the tooltip
                var matches = Regex.Matches(completionDetails.ToolTipText, @"^\[(.+)\]");

                if (matches.Count > 0 && matches[0].Groups.Count > 1)
                {
                    detailString = matches[0].Groups[1].Value;
                }
            }

            return new CompletionItem
            {
                Label = completionDetails.CompletionText,
                Kind = MapCompletionKind(completionDetails.CompletionType),
                Detail = detailString,
                TextEdit = new TextEdit
                {
                    NewText = completionDetails.CompletionText,
                    Range = new Range
                    {
                        Start = new Position
                        {
                            Line = lineNumber,
                            Character = startColumn
                        },
                        End = new Position
                        {
                            Line = lineNumber,
                            Character = endColumn
                        }
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

        private static ParameterInformation CreateParameterInfo(ParameterInfo parameterInfo)
        {
            return new ParameterInformation
            {
                Label = parameterInfo.Name,
                Documentation = string.Empty
            };
        }

        #endregion
    }
}

