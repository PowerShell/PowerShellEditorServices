//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Debugging;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Templates;
using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json.Linq;
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
    public class LanguageServer
    {
        private static CancellationTokenSource existingRequestCancellation;

        private ILogger Logger;
        private bool profilesLoaded;
        private bool consoleReplStarted;
        private EditorSession editorSession;
        private IMessageSender messageSender;
        private IMessageHandlers messageHandlers;
        private LanguageServerEditorOperations editorOperations;
        private LanguageServerSettings currentSettings = new LanguageServerSettings();

        private Dictionary<string, Dictionary<string, MarkerCorrection>> codeActionsPerFile =
            new Dictionary<string, Dictionary<string, MarkerCorrection>>();

        private TaskCompletionSource<bool> serverCompletedTask;

        public IEditorOperations EditorOperations
        {
            get { return this.editorOperations; }
        }

        /// <summary>
        /// Initializes a new language server that is used for handing language server protocol messages
        /// </summary>
        /// <param name="editorSession">The editor session that handles the PowerShell runspace</param>
        /// <param name="messageHandlers">An object that manages all of the message handlers</param>
        /// <param name="messageSender">The message sender</param>
        /// <param name="serverCompletedTask">A TaskCompletionSource<bool> that will be completed to stop the running process</param>
        /// <param name="logger">The logger.</param>
        public LanguageServer(
            EditorSession editorSession,
            IMessageHandlers messageHandlers,
            IMessageSender messageSender,
            TaskCompletionSource<bool> serverCompletedTask,
            ILogger logger)
        {
            this.Logger = logger;
            this.editorSession = editorSession;
            this.serverCompletedTask = serverCompletedTask;
            // Attach to the underlying PowerShell context to listen for changes in the runspace or execution status
            this.editorSession.PowerShellContext.RunspaceChanged += PowerShellContext_RunspaceChanged;
            this.editorSession.PowerShellContext.ExecutionStatusChanged += PowerShellContext_ExecutionStatusChanged;

            // Attach to ExtensionService events
            this.editorSession.ExtensionService.CommandAdded += ExtensionService_ExtensionAdded;
            this.editorSession.ExtensionService.CommandUpdated += ExtensionService_ExtensionUpdated;
            this.editorSession.ExtensionService.CommandRemoved += ExtensionService_ExtensionRemoved;

            this.messageSender = messageSender;
            this.messageHandlers = messageHandlers;

            // Create the IEditorOperations implementation
            this.editorOperations =
                new LanguageServerEditorOperations(
                    this.editorSession,
                    this.messageSender);

            this.editorSession.StartDebugService(this.editorOperations);
            this.editorSession.DebugService.DebuggerStopped += DebugService_DebuggerStopped;
        }

        /// <summary>
        /// Starts the language server client and sends the Initialize method.
        /// </summary>
        /// <returns>A Task that can be awaited for initialization to complete.</returns>
        public void Start()
        {
            // Register all supported message types

            this.messageHandlers.SetRequestHandler(ShutdownRequest.Type, this.HandleShutdownRequest);
            this.messageHandlers.SetEventHandler(ExitNotification.Type, this.HandleExitNotification);

            this.messageHandlers.SetRequestHandler(InitializeRequest.Type, this.HandleInitializeRequest);
            this.messageHandlers.SetEventHandler(InitializedNotification.Type, this.HandleInitializedNotification);

            this.messageHandlers.SetEventHandler(DidOpenTextDocumentNotification.Type, this.HandleDidOpenTextDocumentNotification);
            this.messageHandlers.SetEventHandler(DidCloseTextDocumentNotification.Type, this.HandleDidCloseTextDocumentNotification);
            this.messageHandlers.SetEventHandler(DidSaveTextDocumentNotification.Type, this.HandleDidSaveTextDocumentNotification);
            this.messageHandlers.SetEventHandler(DidChangeTextDocumentNotification.Type, this.HandleDidChangeTextDocumentNotification);
            this.messageHandlers.SetEventHandler(DidChangeConfigurationNotification<LanguageServerSettingsWrapper>.Type, this.HandleDidChangeConfigurationNotification);

            this.messageHandlers.SetRequestHandler(DefinitionRequest.Type, this.HandleDefinitionRequest);
            this.messageHandlers.SetRequestHandler(ReferencesRequest.Type, this.HandleReferencesRequest);
            this.messageHandlers.SetRequestHandler(CompletionRequest.Type, this.HandleCompletionRequest);
            this.messageHandlers.SetRequestHandler(CompletionResolveRequest.Type, this.HandleCompletionResolveRequest);
            this.messageHandlers.SetRequestHandler(SignatureHelpRequest.Type, this.HandleSignatureHelpRequest);
            this.messageHandlers.SetRequestHandler(DocumentHighlightRequest.Type, this.HandleDocumentHighlightRequest);
            this.messageHandlers.SetRequestHandler(HoverRequest.Type, this.HandleHoverRequest);
            this.messageHandlers.SetRequestHandler(WorkspaceSymbolRequest.Type, this.HandleWorkspaceSymbolRequest);
            this.messageHandlers.SetRequestHandler(CodeActionRequest.Type, this.HandleCodeActionRequest);
            this.messageHandlers.SetRequestHandler(DocumentFormattingRequest.Type, this.HandleDocumentFormattingRequest);
            this.messageHandlers.SetRequestHandler(
                DocumentRangeFormattingRequest.Type,
                this.HandleDocumentRangeFormattingRequest);

            this.messageHandlers.SetRequestHandler(ShowOnlineHelpRequest.Type, this.HandleShowOnlineHelpRequest);
            this.messageHandlers.SetRequestHandler(ExpandAliasRequest.Type, this.HandleExpandAliasRequest);

            this.messageHandlers.SetRequestHandler(FindModuleRequest.Type, this.HandleFindModuleRequest);
            this.messageHandlers.SetRequestHandler(InstallModuleRequest.Type, this.HandleInstallModuleRequest);

            this.messageHandlers.SetRequestHandler(InvokeExtensionCommandRequest.Type, this.HandleInvokeExtensionCommandRequest);

            this.messageHandlers.SetRequestHandler(PowerShellVersionRequest.Type, this.HandlePowerShellVersionRequest);

            this.messageHandlers.SetRequestHandler(NewProjectFromTemplateRequest.Type, this.HandleNewProjectFromTemplateRequest);
            this.messageHandlers.SetRequestHandler(GetProjectTemplatesRequest.Type, this.HandleGetProjectTemplatesRequest);

            this.messageHandlers.SetRequestHandler(DebugAdapterMessages.EvaluateRequest.Type, this.HandleEvaluateRequest);

            this.messageHandlers.SetRequestHandler(GetPSSARulesRequest.Type, this.HandleGetPSSARulesRequest);
            this.messageHandlers.SetRequestHandler(SetPSSARulesRequest.Type, this.HandleSetPSSARulesRequest);

            this.messageHandlers.SetRequestHandler(ScriptRegionRequest.Type, this.HandleGetFormatScriptRegionRequest);

            this.messageHandlers.SetRequestHandler(GetPSHostProcessesRequest.Type, this.HandleGetPSHostProcessesRequest);
            this.messageHandlers.SetRequestHandler(CommentHelpRequest.Type, this.HandleCommentHelpRequest);

            // Initialize the extension service
            // TODO: This should be made awaited once Initialize is async!
            this.editorSession.ExtensionService.Initialize(
                this.editorOperations,
                this.editorSession.Components).Wait();
        }

        protected Task Stop()
        {
            Logger.Write(LogLevel.Normal, "Language service is shutting down...");

            // complete the task so that the host knows to shut down
            this.serverCompletedTask.SetResult(true);

            return Task.FromResult(true);
        }

        #region Built-in Message Handlers

        private async Task HandleShutdownRequest(
            RequestContext<object> requestContext)
        {
            // Allow the implementor to shut down gracefully

            await requestContext.SendResult(new object());
        }

        private async Task HandleExitNotification(
            object exitParams,
            EventContext eventContext)
        {
            // Stop the server channel
            await this.Stop();
        }

        private Task HandleInitializedNotification(InitializedParams initializedParams,
            EventContext eventContext)
        {
            // Can do dynamic registration of capabilities in this notification handler
            return Task.FromResult(true);
        }

        protected async Task HandleInitializeRequest(
            InitializeParams initializeParams,
            RequestContext<InitializeResult> requestContext)
        {
            // Grab the workspace path from the parameters
            editorSession.Workspace.WorkspacePath = initializeParams.RootPath;

            // Set the working directory of the PowerShell session to the workspace path
            if (editorSession.Workspace.WorkspacePath != null)
            {
                await editorSession.PowerShellContext.SetWorkingDirectory(
                    editorSession.Workspace.WorkspacePath,
                    isPathAlreadyEscaped: false);
            }

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
                        CodeActionProvider = true,
                        CodeLensProvider = new CodeLensOptions { ResolveProvider = true },
                        CompletionProvider = new CompletionOptions
                        {
                            ResolveProvider = true,
                            TriggerCharacters = new string[] { ".", "-", ":", "\\" }
                        },
                        SignatureHelpProvider = new SignatureHelpOptions
                        {
                            TriggerCharacters = new string[] { " " } // TODO: Other characters here?
                        },
                        DocumentFormattingProvider = false
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

        private async Task HandleSetPSSARulesRequest(
            object param,
            RequestContext<object> requestContext)
        {
            var dynParams = param as dynamic;
            if (editorSession.AnalysisService != null &&
                    editorSession.AnalysisService.SettingsPath == null)
            {
                var activeRules = new List<string>();
                var ruleInfos = dynParams.ruleInfos;
                foreach (dynamic ruleInfo in ruleInfos)
                {
                    if ((Boolean)ruleInfo.isEnabled)
                    {
                        activeRules.Add((string)ruleInfo.name);
                    }
                }
                editorSession.AnalysisService.ActiveRules = activeRules.ToArray();
            }

            var sendresult = requestContext.SendResult(null);
            var scripFile = editorSession.Workspace.GetFile((string)dynParams.filepath);
            await RunScriptDiagnostics(
                    new ScriptFile[] { scripFile },
                        editorSession,
                        this.messageSender.SendEvent);
            await sendresult;
        }

        private async Task HandleGetFormatScriptRegionRequest(
            ScriptRegionRequestParams requestParams,
            RequestContext<ScriptRegionRequestResult> requestContext)
        {
            var scriptFile = this.editorSession.Workspace.GetFile(requestParams.FileUri);
            var lineNumber = requestParams.Line;
            var columnNumber = requestParams.Column;
            ScriptRegion scriptRegion = null;

            switch (requestParams.Character)
            {
                case "\n":
                    // find the smallest statement ast that occupies
                    // the element before \n or \r\n and return the extent.
                    --lineNumber; // vscode sends the next line when pressed enter
                    var line = scriptFile.GetLine(lineNumber);
                    if (!String.IsNullOrEmpty(line))
                    {
                        scriptRegion = this.editorSession.LanguageService.FindSmallestStatementAstRegion(
                            scriptFile,
                            lineNumber,
                            line.Length);
                    }
                    break;

                case "}":
                    scriptRegion = this.editorSession.LanguageService.FindSmallestStatementAstRegion(
                            scriptFile,
                            lineNumber,
                            columnNumber);
                    break;

                default:
                    break;
            }

            await requestContext.SendResult(new ScriptRegionRequestResult
            {
                scriptRegion = scriptRegion
            });
        }

        private async Task HandleGetPSSARulesRequest(
            object param,
            RequestContext<object> requestContext)
        {
            List<object> rules = null;
            if (editorSession.AnalysisService != null
                    && editorSession.AnalysisService.SettingsPath == null)
            {
                rules = new List<object>();
                var ruleNames = editorSession.AnalysisService.GetPSScriptAnalyzerRules();
                var activeRules = editorSession.AnalysisService.ActiveRules;
                foreach (var ruleName in ruleNames)
                {
                    rules.Add(new { name = ruleName, isEnabled = activeRules.Contains(ruleName, StringComparer.OrdinalIgnoreCase) });
                }
            }

            await requestContext.SendResult(rules);
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
            // We don't await the result of the execution here because we want
            // to be able to receive further messages while the editor command
            // is executing.  This important in cases where the pipeline thread
            // gets blocked by something in the script like a prompt to the user.
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

            return Task.FromResult(true);
        }

        private Task HandleNewProjectFromTemplateRequest(
            NewProjectFromTemplateRequest newProjectArgs,
            RequestContext<NewProjectFromTemplateResponse> requestContext)
        {
            // Don't await the Task here so that we don't block the session
            this.editorSession.TemplateService
                .CreateFromTemplate(newProjectArgs.TemplatePath, newProjectArgs.DestinationPath)
                .ContinueWith(
                    async task =>
                    {
                        await requestContext.SendResult(
                            new NewProjectFromTemplateResponse
                            {
                                CreationSuccessful = task.Result
                            });
                    });

            return Task.FromResult(true);
        }

        private async Task HandleGetProjectTemplatesRequest(
            GetProjectTemplatesRequest requestArgs,
            RequestContext<GetProjectTemplatesResponse> requestContext)
        {
            bool plasterInstalled = await this.editorSession.TemplateService.ImportPlasterIfInstalled();

            if (plasterInstalled)
            {
                var availableTemplates =
                    await this.editorSession.TemplateService.GetAvailableTemplates(
                        requestArgs.IncludeInstalledModules);

                await requestContext.SendResult(
                    new GetProjectTemplatesResponse
                    {
                        Templates = availableTemplates
                    });
            }
            else
            {
                await requestContext.SendResult(
                    new GetProjectTemplatesResponse
                    {
                        NeedsModuleInstall = true,
                        Templates = new TemplateDetails[0]
                    });
            }
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
                    Sort-Object Start -Descending

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
            DidOpenTextDocumentParams openParams,
            EventContext eventContext)
        {
            ScriptFile openedFile =
                editorSession.Workspace.GetFileBuffer(
                    openParams.TextDocument.Uri,
                    openParams.TextDocument.Text);

            // TODO: Get all recently edited files in the workspace
            this.RunScriptDiagnostics(
                new ScriptFile[] { openedFile },
                editorSession,
                eventContext);

            Logger.Write(LogLevel.Verbose, "Finished opening document.");

            return Task.FromResult(true);
        }

        protected async Task HandleDidCloseTextDocumentNotification(
            DidCloseTextDocumentParams closeParams,
            EventContext eventContext)
        {
            // Find and close the file in the current session
            var fileToClose = editorSession.Workspace.GetFile(closeParams.TextDocument.Uri);

            if (fileToClose != null)
            {
                editorSession.Workspace.CloseFile(fileToClose);
                await ClearMarkers(fileToClose, eventContext);
            }

            Logger.Write(LogLevel.Verbose, "Finished closing document.");
        }
        protected async Task HandleDidSaveTextDocumentNotification(
            DidSaveTextDocumentParams saveParams,
            EventContext eventContext)
        {
            ScriptFile savedFile =
                this.editorSession.Workspace.GetFile(
                    saveParams.TextDocument.Uri);

            if (savedFile != null)
            {
                if (this.editorSession.RemoteFileManager.IsUnderRemoteTempPath(savedFile.FilePath))
                {
                    await this.editorSession.RemoteFileManager.SaveRemoteFile(
                        savedFile.FilePath);
                }
            }
        }

        protected Task HandleDidChangeTextDocumentNotification(
            DidChangeTextDocumentParams textChangeParams,
            EventContext eventContext)
        {
            List<ScriptFile> changedFiles = new List<ScriptFile>();

            // A text change notification can batch multiple change requests
            foreach (var textChange in textChangeParams.ContentChanges)
            {
                ScriptFile changedFile = editorSession.Workspace.GetFile(textChangeParams.TextDocument.Uri);

                changedFile.ApplyChange(
                    GetFileChangeDetails(
                        textChange.Range,
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
                this.currentSettings.ScriptAnalysis.Enable.HasValue ? this.currentSettings.ScriptAnalysis.Enable.Value : false;
            string oldScriptAnalysisSettingsPath =
                this.currentSettings.ScriptAnalysis.SettingsPath;

            this.currentSettings.Update(
                configChangeParams.Settings.Powershell,
                this.editorSession.Workspace.WorkspacePath,
                this.Logger);

            if (!this.profilesLoaded &&
                this.currentSettings.EnableProfileLoading &&
                oldLoadProfiles != this.currentSettings.EnableProfileLoading)
            {
                await this.editorSession.PowerShellContext.LoadHostProfiles();
                this.profilesLoaded = true;
            }

            // Wait until after profiles are loaded (or not, if that's the
            // case) before starting the interactive console.
            if (!this.consoleReplStarted)
            {
                // Start the interactive terminal
                this.editorSession.HostInput.StartCommandLoop();
                this.consoleReplStarted = true;
            }

            // If there is a new settings file path, restart the analyzer with the new settigs.
            bool settingsPathChanged = false;
            string newSettingsPath = this.currentSettings.ScriptAnalysis.SettingsPath;
            if (!string.Equals(oldScriptAnalysisSettingsPath, newSettingsPath, StringComparison.OrdinalIgnoreCase))
            {
                this.editorSession.AnalysisService.SettingsPath = newSettingsPath;
                settingsPathChanged = true;
            }

            // If script analysis settings have changed we need to clear & possibly update the current diagnostic records.
            if ((oldScriptAnalysisEnabled != this.currentSettings.ScriptAnalysis.Enable) || settingsPathChanged)
            {
                // If the user just turned off script analysis or changed the settings path, send a diagnostics
                // event to clear the analysis markers that they already have.
                if (!this.currentSettings.ScriptAnalysis.Enable.Value || settingsPathChanged)
                {
                    foreach (var scriptFile in editorSession.Workspace.GetOpenedFiles())
                    {
                        await ClearMarkers(scriptFile, eventContext);
                    }
                }

                await this.RunScriptDiagnostics(
                    this.editorSession.Workspace.GetOpenedFiles(),
                    this.editorSession,
                    eventContext);
            }
        }

        protected async Task HandleDefinitionRequest(
            TextDocumentPositionParams textDocumentPosition,
            RequestContext<Location[]> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPosition.TextDocument.Uri);

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
                            Uri = GetFileUri(definition.FoundDefinition.FilePath),
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
                    referencesParams.TextDocument.Uri);

            SymbolReference foundSymbol =
                editorSession.LanguageService.FindSymbolAtLocation(
                    scriptFile,
                    referencesParams.Position.Line + 1,
                    referencesParams.Position.Character + 1);

            FindReferencesResult referencesResult =
                await editorSession.LanguageService.FindReferencesOfSymbol(
                    foundSymbol,
                    editorSession.Workspace.ExpandScriptReferences(scriptFile),
                    editorSession.Workspace);

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
                                    Uri = GetFileUri(r.FilePath),
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
            TextDocumentPositionParams textDocumentPositionParams,
            RequestContext<CompletionItem[]> requestContext)
        {
            int cursorLine = textDocumentPositionParams.Position.Line + 1;
            int cursorColumn = textDocumentPositionParams.Position.Character + 1;

            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPositionParams.TextDocument.Uri);

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
                // Get the documentation for the function
                CommandInfo commandInfo =
                    await CommandHelpers.GetCommandInfo(
                        completionItem.Label,
                        this.editorSession.PowerShellContext);

                if (commandInfo != null)
                {
                    completionItem.Documentation =
                        await CommandHelpers.GetCommandSynopsis(
                            commandInfo,
                            this.editorSession.PowerShellContext);
                }
            }

            // Send back the updated CompletionItem
            await requestContext.SendResult(completionItem);
        }

        protected async Task HandleSignatureHelpRequest(
            TextDocumentPositionParams textDocumentPositionParams,
            RequestContext<SignatureHelp> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPositionParams.TextDocument.Uri);

            ParameterSetSignatures parameterSets =
                await editorSession.LanguageService.FindParameterSetsInFile(
                    scriptFile,
                    textDocumentPositionParams.Position.Line + 1,
                    textDocumentPositionParams.Position.Character + 1);

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
            TextDocumentPositionParams textDocumentPositionParams,
            RequestContext<DocumentHighlight[]> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPositionParams.TextDocument.Uri);

            FindOccurrencesResult occurrencesResult =
                editorSession.LanguageService.FindOccurrencesInFile(
                    scriptFile,
                    textDocumentPositionParams.Position.Line + 1,
                    textDocumentPositionParams.Position.Character + 1);

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
            TextDocumentPositionParams textDocumentPositionParams,
            RequestContext<Hover> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPositionParams.TextDocument.Uri);

            SymbolDetails symbolDetails =
                await editorSession
                    .LanguageService
                    .FindSymbolDetailsAtLocation(
                        scriptFile,
                        textDocumentPositionParams.Position.Line + 1,
                        textDocumentPositionParams.Position.Character + 1);

            List<MarkedString> symbolInfo = new List<MarkedString>();
            Range symbolRange = null;

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
            DocumentSymbolParams documentSymbolParams,
            RequestContext<SymbolInformation[]> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    documentSymbolParams.TextDocument.Uri);

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
                                        Uri = GetFileUri(r.FilePath),
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

        public static SymbolKind GetSymbolKind(SymbolType symbolType)
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

        public static string GetDecoratedSymbolName(SymbolReference symbolReference)
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
                                            Uri = GetFileUri(r.FilePath),
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

        protected async Task HandlePowerShellVersionRequest(
            object noParams,
            RequestContext<PowerShellVersion> requestContext)
        {
            await requestContext.SendResult(
                new PowerShellVersion(
                    this.editorSession.PowerShellContext.LocalPowerShellVersion));
        }

        protected async Task HandleGetPSHostProcessesRequest(
            object noParams,
            RequestContext<GetPSHostProcessesResponse[]> requestContext)
        {
            var psHostProcesses = new List<GetPSHostProcessesResponse>();

            if (this.editorSession.PowerShellContext.LocalPowerShellVersion.Version.Major >= 5)
            {
                int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var psCommand = new PSCommand();
                psCommand.AddCommand("Get-PSHostProcessInfo");
                psCommand.AddCommand("Where-Object")
                    .AddParameter("Property", "ProcessId")
                    .AddParameter("NE")
                    .AddParameter("Value", processId.ToString());

                var processes = await editorSession.PowerShellContext.ExecuteCommand<PSObject>(psCommand);
                if (processes != null)
                {
                    foreach (dynamic p in processes)
                    {
                        psHostProcesses.Add(
                            new GetPSHostProcessesResponse
                            {
                                ProcessName = p.ProcessName,
                                ProcessId = p.ProcessId,
                                AppDomainName = p.AppDomainName,
                                MainWindowTitle = p.MainWindowTitle
                            });
                    }
                }
            }

            await requestContext.SendResult(psHostProcesses.ToArray());
        }

        protected async Task HandleCommentHelpRequest(
           CommentHelpRequestParams requestParams,
           RequestContext<CommentHelpRequestResult> requestContext)
        {
            var scriptFile = this.editorSession.Workspace.GetFile(requestParams.DocumentUri);
            var triggerLine0b = requestParams.TriggerPosition.Line;
            var triggerLine1b = triggerLine0b + 1;

            string helpLocation;
            var functionDefinitionAst = editorSession.LanguageService.GetFunctionDefinitionForHelpComment(
                scriptFile,
                triggerLine1b,
                out helpLocation);
            var result = new CommentHelpRequestResult();
            if (functionDefinitionAst != null)
            {
                var funcExtent = functionDefinitionAst.Extent;
                var funcText = funcExtent.Text;
                if (helpLocation.Equals("begin"))
                {
                    // check if the previous character is `<` because it invalidates
                    // the param block the follows it.
                    var lines = ScriptFile.GetLines(funcText).ToArray();
                    var relativeTriggerLine0b = triggerLine1b - funcExtent.StartLineNumber;
                    if (relativeTriggerLine0b > 0 && lines[relativeTriggerLine0b].IndexOf("<") > -1)
                    {
                        lines[relativeTriggerLine0b] = string.Empty;
                    }

                    funcText = string.Join("\n", lines);
                }

                var analysisResults = await this.editorSession.AnalysisService.GetSemanticMarkersAsync(
                    funcText,
                    AnalysisService.GetCommentHelpRuleSettings(
                        true,
                        false,
                        requestParams.BlockComment,
                        true,
                        helpLocation));
                var help = analysisResults?.FirstOrDefault()?.Correction?.Edits[0].Text;
                result.Content = help == null ? null : ScriptFile.GetLines(help).ToArray();
                if (helpLocation != null &&
                    !helpLocation.Equals("before", StringComparison.OrdinalIgnoreCase))
                {
                    // we need to trim the leading `{` and newline when helpLocation=="begin"
                    // we also need to trim the leading newline when helpLocation=="end"
                    result.Content = result.Content?.Skip(1).ToArray();
                }
            }

            await requestContext.SendResult(result);
        }

        private bool IsQueryMatch(string query, string symbolName)
        {
            return symbolName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected async Task HandleCodeActionRequest(
            CodeActionParams codeActionParams,
            RequestContext<CodeActionCommand[]> requestContext)
        {
            MarkerCorrection correction = null;
            Dictionary<string, MarkerCorrection> markerIndex = null;
            List<CodeActionCommand> codeActionCommands = new List<CodeActionCommand>();

            if (this.codeActionsPerFile.TryGetValue(codeActionParams.TextDocument.Uri, out markerIndex))
            {
                foreach (var diagnostic in codeActionParams.Context.Diagnostics)
                {
                    if (!string.IsNullOrEmpty(diagnostic.Code) &&
                        markerIndex.TryGetValue(diagnostic.Code, out correction))
                    {
                        codeActionCommands.Add(
                            new CodeActionCommand
                            {
                                Title = correction.Name,
                                Command = "PowerShell.ApplyCodeActionEdits",
                                Arguments = JArray.FromObject(correction.Edits)
                            });
                    }
                }
            }

            await requestContext.SendResult(
                codeActionCommands.ToArray());
        }

        protected async Task HandleDocumentFormattingRequest(
            DocumentFormattingParams formattingParams,
            RequestContext<TextEdit[]> requestContext)
        {
            var result = await Format(
                formattingParams.TextDocument.Uri,
                formattingParams.options,
                null);

            await requestContext.SendResult(new TextEdit[1]
            {
                new TextEdit
                {
                    NewText = result.Item1,
                    Range = result.Item2
                },
            });
        }

        protected async Task HandleDocumentRangeFormattingRequest(
            DocumentRangeFormattingParams formattingParams,
            RequestContext<TextEdit[]> requestContext)
        {
            var result = await Format(
                formattingParams.TextDocument.Uri,
                formattingParams.Options,
                formattingParams.Range);

            await requestContext.SendResult(new TextEdit[1]
            {
                new TextEdit
                {
                    NewText = result.Item1,
                    Range = result.Item2
                },
            });
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
                    writeInputToHost: true,
                    writeOutputToHost: true,
                    addToHistory: true);

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

        private async Task<Tuple<string, Range>> Format(
            string documentUri,
            FormattingOptions options,
            Range range)
        {
            var scriptFile = editorSession.Workspace.GetFile(documentUri);
            var pssaSettings = currentSettings.CodeFormatting.GetPSSASettingsHashtable(
                options.TabSize,
                options.InsertSpaces);

            // TODO raise an error event in case format returns null;
            string formattedScript;
            Range editRange;
            var rangeList = range == null ? null : new int[] {
                range.Start.Line + 1,
                range.Start.Character + 1,
                range.End.Line + 1,
                range.End.Character + 1};
            var extent = scriptFile.ScriptAst.Extent;

            // todo create an extension for converting range to script extent
            editRange = new Range
            {
                Start = new Position
                {
                    Line = extent.StartLineNumber - 1,
                    Character = extent.StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = extent.EndLineNumber - 1,
                    Character = extent.EndColumnNumber - 1
                }
            };

            formattedScript = await editorSession.AnalysisService.Format(
                scriptFile.Contents,
                pssaSettings,
                rangeList);
            formattedScript = formattedScript ?? scriptFile.Contents;
            return Tuple.Create(formattedScript, editRange);
        }

        private async void PowerShellContext_RunspaceChanged(object sender, Session.RunspaceChangedEventArgs e)
        {
            await this.messageSender.SendEvent(
                RunspaceChangedEvent.Type,
                new Protocol.LanguageServer.RunspaceDetails(e.NewRunspace));
        }

        /// <summary>
        /// Event hook on the PowerShell context to listen for changes in script execution status
        /// </summary>
        /// <param name="sender">the PowerShell context sending the execution event</param>
        /// <param name="e">details of the execution status change</param>
        private async void PowerShellContext_ExecutionStatusChanged(object sender, ExecutionStatusChangedEventArgs e)
        {
            await this.messageSender.SendEvent(
                ExecutionStatusChangedEvent.Type,
                e);
        }

        private async void ExtensionService_ExtensionAdded(object sender, EditorCommand e)
        {
            await this.messageSender.SendEvent(
                ExtensionCommandAddedNotification.Type,
                new ExtensionCommandAddedNotification
                {
                    Name = e.Name,
                    DisplayName = e.DisplayName
                });
        }

        private async void ExtensionService_ExtensionUpdated(object sender, EditorCommand e)
        {
            await this.messageSender.SendEvent(
                ExtensionCommandUpdatedNotification.Type,
                new ExtensionCommandUpdatedNotification
                {
                    Name = e.Name,
                });
        }

        private async void ExtensionService_ExtensionRemoved(object sender, EditorCommand e)
        {
            await this.messageSender.SendEvent(
                ExtensionCommandRemovedNotification.Type,
                new ExtensionCommandRemovedNotification
                {
                    Name = e.Name,
                });
        }

        private async void DebugService_DebuggerStopped(object sender, DebuggerStoppedEventArgs e)
        {
            if (!this.editorSession.DebugService.IsClientAttached)
            {
                await this.messageSender.SendEvent(
                    StartDebuggerEvent.Type,
                    new StartDebuggerEvent());
            }
        }

        #endregion

        #region Helper Methods

        public static string GetFileUri(string filePath)
        {
            // If the file isn't untitled, return a URI-style path
            return
                !filePath.StartsWith("untitled")
                    ? new Uri("file://" + filePath).AbsoluteUri
                    : filePath;
        }

        public static Range GetRangeFromScriptRegion(ScriptRegion scriptRegion)
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

            if (changeRange == null) return new FileChange { InsertString = insertString, IsReload = true };

            return new FileChange
            {
                InsertString = insertString,
                Line = changeRange.Start.Line + 1,
                Offset = changeRange.Start.Character + 1,
                EndLine = changeRange.End.Line + 1,
                EndOffset = changeRange.End.Character + 1,
                IsReload = false
            };
        }

        private Task RunScriptDiagnostics(
            ScriptFile[] filesToAnalyze,
            EditorSession editorSession,
            EventContext eventContext)
        {
            return RunScriptDiagnostics(filesToAnalyze, editorSession, this.messageSender.SendEvent);
        }

        private Task RunScriptDiagnostics(
            ScriptFile[] filesToAnalyze,
            EditorSession editorSession,
            Func<NotificationType<PublishDiagnosticsNotification, object>, PublishDiagnosticsNotification, Task> eventSender)
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
                        "Exception while canceling analysis task:\n\n{0}",
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
                        this.currentSettings.ScriptAnalysis.Enable.Value,
                        this.codeActionsPerFile,
                        editorSession,
                        eventSender,
                        this.Logger,
                        existingRequestCancellation.Token),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);

            return Task.FromResult(true);
        }

        private static async Task DelayThenInvokeDiagnostics(
            int delayMilliseconds,
            ScriptFile[] filesToAnalyze,
            bool isScriptAnalysisEnabled,
            Dictionary<string, Dictionary<string, MarkerCorrection>> correctionIndex,
            EditorSession editorSession,
            EventContext eventContext,
            ILogger Logger,
            CancellationToken cancellationToken)
        {
            await DelayThenInvokeDiagnostics(
                delayMilliseconds,
                filesToAnalyze,
                isScriptAnalysisEnabled,
                correctionIndex,
                editorSession,
                eventContext.SendEvent,
                Logger,
                cancellationToken);
        }


        private static async Task DelayThenInvokeDiagnostics(
            int delayMilliseconds,
            ScriptFile[] filesToAnalyze,
            bool isScriptAnalysisEnabled,
            Dictionary<string, Dictionary<string, MarkerCorrection>> correctionIndex,
            EditorSession editorSession,
            Func<NotificationType<PublishDiagnosticsNotification, object>, PublishDiagnosticsNotification, Task> eventSender,
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
                if (isScriptAnalysisEnabled && editorSession.AnalysisService != null)
                {
                    Logger.Write(LogLevel.Verbose, "Analyzing script file: " + scriptFile.FilePath);

                    semanticMarkers = await editorSession.AnalysisService.GetSemanticMarkersAsync(scriptFile);

                    Logger.Write(LogLevel.Verbose, "Analysis complete.");
                }
                else
                {
                    // Semantic markers aren't available if the AnalysisService
                    // isn't available
                    semanticMarkers = new ScriptFileMarker[0];
                }

                await PublishScriptDiagnostics(
                    scriptFile,
                    scriptFile.SyntaxMarkers.Concat(semanticMarkers).ToArray(),
                    correctionIndex,
                    eventSender);
            }
        }

        private async Task ClearMarkers(ScriptFile scriptFile, EventContext eventContext)
        {
            // send empty diagnostic markers to clear any markers associated with the given file
            await PublishScriptDiagnostics(
                    scriptFile,
                    new ScriptFileMarker[0],
                    this.codeActionsPerFile,
                    eventContext);
        }

        private static async Task PublishScriptDiagnostics(
            ScriptFile scriptFile,
            ScriptFileMarker[] markers,
            Dictionary<string, Dictionary<string, MarkerCorrection>> correctionIndex,
            EventContext eventContext)
        {
            await PublishScriptDiagnostics(
                scriptFile,
                markers,
                correctionIndex,
                eventContext.SendEvent);
        }

        private static async Task PublishScriptDiagnostics(
            ScriptFile scriptFile,
            ScriptFileMarker[] markers,
            Dictionary<string, Dictionary<string, MarkerCorrection>> correctionIndex,
            Func<NotificationType<PublishDiagnosticsNotification, object>, PublishDiagnosticsNotification, Task> eventSender)
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
                    fileCorrections.Add(markerDiagnostic.Code, marker.Correction);
                }

                diagnostics.Add(markerDiagnostic);
            }

            correctionIndex[scriptFile.ClientFilePath] = fileCorrections;

            // Always send syntax and semantic errors.  We want to
            // make sure no out-of-date markers are being displayed.
            await eventSender(
                PublishDiagnosticsNotification.Type,
                new PublishDiagnosticsNotification
                {
                    Uri = scriptFile.ClientFilePath,
                    Diagnostics = diagnostics.ToArray()
                });
        }

        private static Diagnostic GetDiagnosticFromMarker(ScriptFileMarker scriptFileMarker)
        {
            return new Diagnostic
            {
                Severity = MapDiagnosticSeverity(scriptFileMarker.Level),
                Message = scriptFileMarker.Message,
                Code = scriptFileMarker.Source + Guid.NewGuid().ToString(),
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

        private static CompletionItemKind MapCompletionKind(CompletionType completionType)
        {
            switch (completionType)
            {
                case CompletionType.Command:
                    return CompletionItemKind.Function;

                case CompletionType.Property:
                    return CompletionItemKind.Property;

                case CompletionType.Method:
                    return CompletionItemKind.Method;

                case CompletionType.Variable:
                case CompletionType.ParameterName:
                    return CompletionItemKind.Variable;

                case CompletionType.File:
                    return CompletionItemKind.File;

                case CompletionType.Folder:
                    return CompletionItemKind.Folder;

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
                    // Fix for #240 - notepad++.exe in tooltip text caused regex parser to throw.
                    string escapedToolTipText = Regex.Escape(completionDetails.ToolTipText);

                    // Don't display ToolTipText if it is the same as the ListItemText.
                    // Reject command syntax ToolTipText - it's too much to display as a detailString.
                    if (!completionDetails.ListItemText.Equals(
                            completionDetails.ToolTipText,
                            StringComparison.OrdinalIgnoreCase) &&
                        !Regex.IsMatch(completionDetails.ToolTipText,
                            @"^\s*" + escapedToolTipText + @"\s+\["))
                    {
                        detailString = completionDetails.ToolTipText;
                    }
                }
            }

            // Force the client to maintain the sort order in which the
            // original completion results were returned. We just need to
            // make sure the default order also be the lexicographical order
            // which we do by prefixing the ListItemText with a leading 0's
            // four digit index.
            var sortText = $"{sortIndex:D4}{completionDetails.ListItemText}";

            return new CompletionItem
            {
                InsertText = completionDetails.CompletionText,
                Label = completionDetails.ListItemText,
                Kind = MapCompletionKind(completionDetails.CompletionType),
                Detail = detailString,
                Documentation = documentationString,
                SortText = sortText,
                FilterText = completionDetails.CompletionText,
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

