//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DebugAdapterMessages = Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class LanguageServer : LanguageServerBase
    {
        private static CancellationTokenSource existingRequestCancellation;

        private bool profilesLoaded;
        private EditorSession editorSession;
        private OutputDebouncer outputDebouncer;
        private LanguageServerEditorOperations editorOperations;
        private LanguageServerSettings currentSettings = new LanguageServerSettings();

        /// <param name="hostDetails">
        /// Provides details about the host application.
        /// </param>
        public LanguageServer(HostDetails hostDetails)
            : this(hostDetails, new StdioServerChannel())
        {
        }

        /// <param name="hostDetails">
        /// Provides details about the host application.
        /// </param>
        public LanguageServer(HostDetails hostDetails, ChannelBase serverChannel)
            : base(serverChannel)
        {
            this.editorSession = new EditorSession();
            this.editorSession.StartSession(hostDetails);
            this.editorSession.ConsoleService.OutputWritten += this.powerShellContext_OutputWritten;

            // Attach to ExtensionService events
            this.editorSession.ExtensionService.CommandAdded += ExtensionService_ExtensionAdded;
            this.editorSession.ExtensionService.CommandUpdated += ExtensionService_ExtensionUpdated;
            this.editorSession.ExtensionService.CommandRemoved += ExtensionService_ExtensionRemoved;

            // Create the IEditorOperations implementation
            this.editorOperations =
                new LanguageServerEditorOperations(
                    this.editorSession,
                    this);

            // Always send console prompts through the UI in the language service
            // TODO: This will change later once we have a general REPL available
            // in VS Code.
            this.editorSession.ConsoleService.PushPromptHandlerContext(
                new ProtocolPromptHandlerContext(
                    this,
                    this.editorSession.ConsoleService));

            // Set up the output debouncer to throttle output event writes
            this.outputDebouncer = new OutputDebouncer(this);
        }

        protected override void Initialize()
        {
            // Initialize the extension service
            // TODO: This should be made awaited once Initialize is async!
            this.editorSession.ExtensionService.Initialize(
                this.editorOperations).Wait();

            // Register all supported message types

            this.SetRequestHandler(InitializeRequest.Type, this.HandleInitializeRequest);

            this.SetEventHandler(DidOpenTextDocumentNotification.Type, this.HandleDidOpenTextDocumentNotification);
            this.SetEventHandler(DidCloseTextDocumentNotification.Type, this.HandleDidCloseTextDocumentNotification);
            this.SetEventHandler(DidChangeTextDocumentNotification.Type, this.HandleDidChangeTextDocumentNotification);
            this.SetEventHandler(DidChangeConfigurationNotification<LanguageServerSettingsWrapper>.Type, this.HandleDidChangeConfigurationNotification);

            this.SetRequestHandler(DefinitionRequest.Type, this.HandleDefinitionRequest);
            this.SetRequestHandler(ReferencesRequest.Type, this.HandleReferencesRequest);
            this.SetRequestHandler(CompletionRequest.Type, this.HandleCompletionRequest);
            this.SetRequestHandler(CompletionResolveRequest.Type, this.HandleCompletionResolveRequest);
            this.SetRequestHandler(SignatureHelpRequest.Type, this.HandleSignatureHelpRequest);
            this.SetRequestHandler(DocumentHighlightRequest.Type, this.HandleDocumentHighlightRequest);
            this.SetRequestHandler(HoverRequest.Type, this.HandleHoverRequest);
            this.SetRequestHandler(DocumentSymbolRequest.Type, this.HandleDocumentSymbolRequest);
            this.SetRequestHandler(WorkspaceSymbolRequest.Type, this.HandleWorkspaceSymbolRequest);

            this.SetRequestHandler(ShowOnlineHelpRequest.Type, this.HandleShowOnlineHelpRequest);
            this.SetRequestHandler(ExpandAliasRequest.Type, this.HandleExpandAliasRequest);

            this.SetRequestHandler(FindModuleRequest.Type, this.HandleFindModuleRequest);
            this.SetRequestHandler(InstallModuleRequest.Type, this.HandleInstallModuleRequest);

            this.SetRequestHandler(InvokeExtensionCommandRequest.Type, this.HandleInvokeExtensionCommandRequest);

            this.SetRequestHandler(DebugAdapterMessages.EvaluateRequest.Type, this.HandleEvaluateRequest);
        }

        protected override async Task Shutdown()
        {
            // Make sure remaining output is flushed before exiting
            await this.outputDebouncer.Flush();

            Logger.Write(LogLevel.Normal, "Language service is shutting down...");

            if (this.editorSession != null)
            {
                this.editorSession.Dispose();
                this.editorSession = null;
            }
        }

        #region Built-in Message Handlers

        protected async Task HandleInitializeRequest(
            InitializeRequest initializeParams,
            RequestContext<InitializeResult> requestContext)
        {
            // Grab the workspace path from the parameters
            editorSession.Workspace.WorkspacePath = initializeParams.RootPath;

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

        protected async Task HandleShowOnlineHelpRequest(
            string helpParams,
            RequestContext<object> requestContext)
        {
            if (helpParams == null) { helpParams = "get-help"; }

            var psCommand = new PSCommand();
            psCommand.AddCommand("Get-Help");
            psCommand.AddArgument(helpParams);
            psCommand.AddParameter("Online");

            await editorSession.PowerShellContext.ExecuteCommand<object>(psCommand);

            await requestContext.SendResult(null);
        }

        private async Task HandleInstallModuleRequest(
            string moduleName,
            RequestContext<object> requestContext
        )
        {
            var script = string.Format("Install-Module -Name {0} -Scope CurrentUser", moduleName);

            var executeTask =
               editorSession.PowerShellContext.ExecuteScriptString(
                   script,
                   true,
                   true).ConfigureAwait(false);

            await requestContext.SendResult(null);
        }

        private Task HandleInvokeExtensionCommandRequest(
            InvokeExtensionCommandRequest commandDetails,
            RequestContext<string> requestContext)
        {
            EditorContext editorContext =
                this.editorOperations.ConvertClientEditorContext(
                    commandDetails.Context);

            Task commandTask =
                this.editorSession.ExtensionService.InvokeCommand(
                    commandDetails.Name,
                    editorContext);

            commandTask.ContinueWith(t =>
            {
                return requestContext.SendResult(null);
            });

            return commandTask;
        }

        private async Task HandleExpandAliasRequest(
            string content,
            RequestContext<string> requestContext)
        {
            var script = @"
function __Expand-Alias {

    param($targetScript)

    [ref]$errors=$null
    
    $tokens = [System.Management.Automation.PsParser]::Tokenize($targetScript, $errors).Where({$_.type -eq 'command'}) | 
                    Sort Start -Descending

    foreach ($token in  $tokens) {
        $definition=(Get-Command ('`'+$token.Content) -CommandType Alias -ErrorAction SilentlyContinue).Definition

        if($definition) {        
            $lhs=$targetScript.Substring(0, $token.Start)
            $rhs=$targetScript.Substring($token.Start + $token.Length)
            
            $targetScript=$lhs + $definition + $rhs
       }
    }

    $targetScript
}";
            var psCommand = new PSCommand();
            psCommand.AddScript(script);
            await this.editorSession.PowerShellContext.ExecuteCommand<PSObject>(psCommand);

            psCommand = new PSCommand();
            psCommand.AddCommand("__Expand-Alias").AddArgument(content);
            var result = await this.editorSession.PowerShellContext.ExecuteCommand<string>(psCommand);

            await requestContext.SendResult(result.First().ToString());
        }

        private async Task HandleFindModuleRequest(
            object param,
            RequestContext<object> requestContext)
        {
            var psCommand = new PSCommand();
            psCommand.AddScript("Find-Module | Select Name, Description");

            var modules = await editorSession.PowerShellContext.ExecuteCommand<PSObject>(psCommand);

            var moduleList = new List<PSModuleMessage>();

            if (modules != null)
            {
                foreach (dynamic m in modules)
                {
                    moduleList.Add(new PSModuleMessage { Name = m.Name, Description = m.Description });
                }
            }

            await requestContext.SendResult(moduleList);
        }

        protected Task HandleDidOpenTextDocumentNotification(
            DidOpenTextDocumentNotification openParams,
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

        protected async Task HandleDidChangeConfigurationNotification(
            DidChangeConfigurationParams<LanguageServerSettingsWrapper> configChangeParams,
            EventContext eventContext)
        {
            bool oldLoadProfiles = this.currentSettings.EnableProfileLoading;
            bool oldScriptAnalysisEnabled = 
                this.currentSettings.ScriptAnalysis.Enable.HasValue;
            string oldScriptAnalysisSettingsPath =
                this.currentSettings.ScriptAnalysis.SettingsPath;

            this.currentSettings.Update(
                configChangeParams.Settings.Powershell, 
                this.editorSession.Workspace.WorkspacePath);

            if (!this.profilesLoaded &&
                this.currentSettings.EnableProfileLoading &&
                oldLoadProfiles != this.currentSettings.EnableProfileLoading)
            {
                await this.editorSession.PowerShellContext.LoadHostProfiles();
                this.profilesLoaded = true;
            }

            // If there is a new settings file path, restart the analyzer with the new settigs.
            bool settingsPathChanged = false;
            string newSettingsPath = this.currentSettings.ScriptAnalysis.SettingsPath;
            if (!string.Equals(oldScriptAnalysisSettingsPath, newSettingsPath, StringComparison.OrdinalIgnoreCase))
            {
                this.editorSession.RestartAnalysisService(newSettingsPath);
                settingsPathChanged = true;
            }

            // If script analysis settings have changed we need to clear & possibly update the current diagnostic records.
            if ((oldScriptAnalysisEnabled != this.currentSettings.ScriptAnalysis.Enable) || settingsPathChanged)
            {
                // If the user just turned off script analysis or changed the settings path, send a diagnostics
                // event to clear the analysis markers that they already have.
                if (!this.currentSettings.ScriptAnalysis.Enable.Value || settingsPathChanged)
                {
                    ScriptFileMarker[] emptyAnalysisDiagnostics = new ScriptFileMarker[0];

                    foreach (var scriptFile in editorSession.Workspace.GetOpenedFiles())
                    {
                        await PublishScriptDiagnostics(
                            scriptFile,
                            emptyAnalysisDiagnostics,
                            eventContext);
                    }
                }

                // If script analysis is enabled and the settings file changed get new diagnostic records.
                if (this.currentSettings.ScriptAnalysis.Enable.Value && settingsPathChanged)
                {
                    await this.RunScriptDiagnostics(
                        this.editorSession.Workspace.GetOpenedFiles(),
                        this.editorSession,
                        eventContext);
                }
            }
        }

        protected async Task HandleDefinitionRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<Location[]> requestContext)
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
            RequestContext<Location[]> requestContext)
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
            RequestContext<CompletionItem[]> requestContext)
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
                int sortIndex = 1;
                completionItems =
                    completionResults
                        .Completions
                        .Select(
                            c => CreateCompletionItem(
                                c,
                                completionResults.ReplacedRange,
                                sortIndex++))
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
            RequestContext<CompletionItem> requestContext)
        {
            if (completionItem.Kind == CompletionItemKind.Function)
            {
                RunspaceHandle runspaceHandle =
                    await editorSession.PowerShellContext.GetRunspaceHandle();

                // Get the documentation for the function
                CommandInfo commandInfo =
                    CommandHelpers.GetCommandInfo(
                        completionItem.Label,
                        runspaceHandle.Runspace);

                completionItem.Documentation =
                    CommandHelpers.GetCommandSynopsis(
                        commandInfo,
                        runspaceHandle.Runspace);

                runspaceHandle.Dispose();
            }

            // Send back the updated CompletionItem
            await requestContext.SendResult(completionItem);
        }

        protected async Task HandleSignatureHelpRequest(
            TextDocumentPosition textDocumentPosition,
            RequestContext<SignatureHelp> requestContext)
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
            RequestContext<DocumentHighlight[]> requestContext)
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
            RequestContext<Hover> requestContext)
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
            RequestContext<SymbolInformation[]> requestContext)
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
            RequestContext<SymbolInformation[]> requestContext)
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

        protected Task HandleEvaluateRequest(
            DebugAdapterMessages.EvaluateRequestArguments evaluateParams,
            RequestContext<DebugAdapterMessages.EvaluateResponseBody> requestContext)
        {
            // We don't await the result of the execution here because we want
            // to be able to receive further messages while the current script
            // is executing.  This important in cases where the pipeline thread
            // gets blocked by something in the script like a prompt to the user.
            var executeTask =
                this.editorSession.PowerShellContext.ExecuteScriptString(
                    evaluateParams.Expression,
                    true,
                    true);

            // Return the execution result after the task completes so that the
            // caller knows when command execution completed.
            executeTask.ContinueWith(
                (task) =>
                {
                    // Return an empty result since the result value is irrelevant
                    // for this request in the LanguageServer
                    return
                        requestContext.SendResult(
                            new DebugAdapterMessages.EvaluateResponseBody
                            {
                                Result = "",
                                VariablesReference = 0
                            });
                });

            return Task.FromResult(true);
        }

        #endregion

        #region Event Handlers

        private async void powerShellContext_OutputWritten(object sender, OutputWrittenEventArgs e)
        {
            // Queue the output for writing
            await this.outputDebouncer.Invoke(e);
        }

        private async void ExtensionService_ExtensionAdded(object sender, EditorCommand e)
        {
            await this.SendEvent(
                ExtensionCommandAddedNotification.Type,
                new ExtensionCommandAddedNotification
                {
                    Name = e.Name,
                    DisplayName = e.DisplayName
                });
        }

        private async void ExtensionService_ExtensionUpdated(object sender, EditorCommand e)
        {
            await this.SendEvent(
                ExtensionCommandUpdatedNotification.Type,
                new ExtensionCommandUpdatedNotification
                {
                    Name = e.Name,
                });
        }

        private async void ExtensionService_ExtensionRemoved(object sender, EditorCommand e)
        {
            await this.SendEvent(
                ExtensionCommandRemovedNotification.Type,
                new ExtensionCommandRemovedNotification
                {
                    Name = e.Name,
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
            if (!this.currentSettings.ScriptAnalysis.Enable.Value)
            {
                // If the user has disabled script analysis, skip it entirely
                return Task.FromResult(true);
            }

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

                TaskCompletionSource<bool> cancelTask = new TaskCompletionSource<bool>();
                cancelTask.SetCanceled();
                return cancelTask.Task;
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

            return Task.FromResult(true);
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
                ScriptFileMarker[] semanticMarkers = null;
                if (editorSession.AnalysisService != null)
                {
                    Logger.Write(LogLevel.Verbose, "Analyzing script file: " + scriptFile.FilePath);

                    semanticMarkers =
                        editorSession.AnalysisService.GetSemanticMarkers(
                            scriptFile);

                    Logger.Write(LogLevel.Verbose, "Analysis complete.");
                }
                else
                {
                    // Semantic markers aren't available if the AnalysisService
                    // isn't available
                    semanticMarkers = new ScriptFileMarker[0];
                }

                var allMarkers = scriptFile.SyntaxMarkers.Concat(semanticMarkers);

                await PublishScriptDiagnostics(
                    scriptFile,
                    semanticMarkers,
                    eventContext);
            }
        }

        private static async Task PublishScriptDiagnostics(
            ScriptFile scriptFile,
            ScriptFileMarker[] semanticMarkers,
            EventContext eventContext)
        {
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
            BufferRange completionRange,
            int sortIndex)
        {
            string detailString = null;
            string documentationString = null;

            if ((completionDetails.CompletionType == CompletionType.Variable) ||
                (completionDetails.CompletionType == CompletionType.ParameterName))
            {
                // Look for type encoded in the tooltip for parameters and variables.
                // Display PowerShell type names in [] to be consistent with PowerShell syntax
                // and now the debugger displays type names.
                var matches = Regex.Matches(completionDetails.ToolTipText, @"^(\[.+\])");
                if ((matches.Count > 0) && (matches[0].Groups.Count > 1))
                {
                    detailString = matches[0].Groups[1].Value;
                }
            }
            else if ((completionDetails.CompletionType == CompletionType.Method) ||
                     (completionDetails.CompletionType == CompletionType.Property))
            {
                // We have a raw signature for .NET members, heck let's display it.  It's
                // better than nothing.
                documentationString = completionDetails.ToolTipText;
            }
            else if (completionDetails.CompletionType == CompletionType.Command)
            {
                // For Commands, let's extract the resolved command or the path for an exe
                // from the ToolTipText - if there is any ToolTipText.
                if (completionDetails.ToolTipText != null)
                {
                    // Don't display ToolTipText if it is the same as the ListItemText.
                    // Reject command syntax ToolTipText - it's too much to display as a detailString.
                    if (!completionDetails.ListItemText.Equals(
                            completionDetails.ToolTipText,
                            StringComparison.OrdinalIgnoreCase) &&
                        !Regex.IsMatch(completionDetails.ToolTipText, 
                            @"^\s*" + completionDetails.ListItemText + @"\s+\["))
                    {
                        detailString = completionDetails.ToolTipText;
                    }
                }
            }

            // We want a special "sort order" for parameters that is not lexicographical.
            // Fortunately, PowerShell returns parameters in the preferred sort order by
            // default (with common params at the end). We just need to make sure the default
            // order also be the lexicographical order which we do by prefixig the ListItemText
            // with a leading 0's four digit index.  This would not sort correctly for a list
            // > 999 parameters but surely we won't have so many items in the "parameter name" 
            // completion list. Technically we don't need the ListItemText at all but it may come
            // in handy during debug.
            var sortText = (completionDetails.CompletionType == CompletionType.ParameterName)
                ? $"{sortIndex:D3}{completionDetails.ListItemText}"
                : null;

            return new CompletionItem
            {
                InsertText = completionDetails.CompletionText,
                Label = completionDetails.ListItemText,
                Kind = MapCompletionKind(completionDetails.CompletionType),
                Detail = detailString,
                Documentation = documentationString,
                SortText = sortText,
                TextEdit = new TextEdit
                {
                    NewText = completionDetails.CompletionText,
                    Range = new Range
                    {
                        Start = new Position
                        {
                            Line = completionRange.Start.Line - 1,
                            Character = completionRange.Start.Column - 1
                        },
                        End = new Position
                        {
                            Line = completionRange.End.Line - 1,
                            Character = completionRange.End.Column - 1
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

