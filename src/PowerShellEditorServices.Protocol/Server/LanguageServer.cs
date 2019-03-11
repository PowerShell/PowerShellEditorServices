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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using DebugAdapterMessages = Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class LanguageServer
    {
        private static CancellationTokenSource s_existingRequestCancellation;

        private static readonly Location[] s_emptyLocationResult = new Location[0];

        private static readonly CompletionItem[] s_emptyCompletionResult = new CompletionItem[0];

        private static readonly SignatureInformation[] s_emptySignatureResult = new SignatureInformation[0];

        private static readonly DocumentHighlight[] s_emptyHighlightResult = new DocumentHighlight[0];

        private static readonly SymbolInformation[] s_emptySymbolResult = new SymbolInformation[0];

        private ILogger Logger;
        private bool profilesLoaded;
        private bool consoleReplStarted;
        private EditorSession editorSession;
        private IMessageSender messageSender;
        private IMessageHandlers messageHandlers;
        private LanguageServerEditorOperations editorOperations;
        private LanguageServerSettings currentSettings = new LanguageServerSettings();

        // The outer key is the file's uri, the inner key is a unique id for the diagnostic
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
            this.editorSession.PowerShellContext.RunspaceChanged += PowerShellContext_RunspaceChangedAsync;
            this.editorSession.PowerShellContext.ExecutionStatusChanged += PowerShellContext_ExecutionStatusChangedAsync;

            // Attach to ExtensionService events
            this.editorSession.ExtensionService.CommandAdded += ExtensionService_ExtensionAddedAsync;
            this.editorSession.ExtensionService.CommandUpdated += ExtensionService_ExtensionUpdatedAsync;
            this.editorSession.ExtensionService.CommandRemoved += ExtensionService_ExtensionRemovedAsync;

            this.messageSender = messageSender;
            this.messageHandlers = messageHandlers;

            // Create the IEditorOperations implementation
            this.editorOperations =
                new LanguageServerEditorOperations(
                    this.editorSession,
                    this.messageSender);

            this.editorSession.StartDebugService(this.editorOperations);
            this.editorSession.DebugService.DebuggerStopped += DebugService_DebuggerStoppedAsync;
        }

        /// <summary>
        /// Starts the language server client and sends the Initialize method.
        /// </summary>
        /// <returns>A Task that can be awaited for initialization to complete.</returns>
        public void Start()
        {
            // Register all supported message types

            this.messageHandlers.SetRequestHandler(ShutdownRequest.Type, this.HandleShutdownRequestAsync);
            this.messageHandlers.SetEventHandler(ExitNotification.Type, this.HandleExitNotificationAsync);

            this.messageHandlers.SetRequestHandler(InitializeRequest.Type, this.HandleInitializeRequestAsync);
            this.messageHandlers.SetEventHandler(InitializedNotification.Type, this.HandleInitializedNotificationAsync);

            this.messageHandlers.SetEventHandler(DidOpenTextDocumentNotification.Type, this.HandleDidOpenTextDocumentNotificationAsync);
            this.messageHandlers.SetEventHandler(DidCloseTextDocumentNotification.Type, this.HandleDidCloseTextDocumentNotificationAsync);
            this.messageHandlers.SetEventHandler(DidSaveTextDocumentNotification.Type, this.HandleDidSaveTextDocumentNotificationAsync);
            this.messageHandlers.SetEventHandler(DidChangeTextDocumentNotification.Type, this.HandleDidChangeTextDocumentNotificationAsync);
            this.messageHandlers.SetEventHandler(DidChangeConfigurationNotification<LanguageServerSettingsWrapper>.Type, this.HandleDidChangeConfigurationNotificationAsync);

            this.messageHandlers.SetRequestHandler(DefinitionRequest.Type, this.HandleDefinitionRequestAsync);
            this.messageHandlers.SetRequestHandler(ReferencesRequest.Type, this.HandleReferencesRequestAsync);
            this.messageHandlers.SetRequestHandler(CompletionRequest.Type, this.HandleCompletionRequestAsync);
            this.messageHandlers.SetRequestHandler(CompletionResolveRequest.Type, this.HandleCompletionResolveRequestAsync);
            this.messageHandlers.SetRequestHandler(SignatureHelpRequest.Type, this.HandleSignatureHelpRequestAsync);
            this.messageHandlers.SetRequestHandler(DocumentHighlightRequest.Type, this.HandleDocumentHighlightRequestAsync);
            this.messageHandlers.SetRequestHandler(HoverRequest.Type, this.HandleHoverRequestAsync);
            this.messageHandlers.SetRequestHandler(WorkspaceSymbolRequest.Type, this.HandleWorkspaceSymbolRequestAsync);
            this.messageHandlers.SetRequestHandler(CodeActionRequest.Type, this.HandleCodeActionRequestAsync);
            this.messageHandlers.SetRequestHandler(DocumentFormattingRequest.Type, this.HandleDocumentFormattingRequestAsync);
            this.messageHandlers.SetRequestHandler(
                DocumentRangeFormattingRequest.Type,
                this.HandleDocumentRangeFormattingRequestAsync);
            this.messageHandlers.SetRequestHandler(FoldingRangeRequest.Type, this.HandleFoldingRangeRequestAsync);

            this.messageHandlers.SetRequestHandler(ShowHelpRequest.Type, this.HandleShowHelpRequestAsync);

            this.messageHandlers.SetRequestHandler(ExpandAliasRequest.Type, this.HandleExpandAliasRequestAsync);
            this.messageHandlers.SetRequestHandler(GetCommandRequest.Type, this.HandleGetCommandRequestAsync);

            this.messageHandlers.SetRequestHandler(FindModuleRequest.Type, this.HandleFindModuleRequestAsync);
            this.messageHandlers.SetRequestHandler(InstallModuleRequest.Type, this.HandleInstallModuleRequestAsync);

            this.messageHandlers.SetRequestHandler(InvokeExtensionCommandRequest.Type, this.HandleInvokeExtensionCommandRequestAsync);

            this.messageHandlers.SetRequestHandler(PowerShellVersionRequest.Type, this.HandlePowerShellVersionRequestAsync);

            this.messageHandlers.SetRequestHandler(NewProjectFromTemplateRequest.Type, this.HandleNewProjectFromTemplateRequestAsync);
            this.messageHandlers.SetRequestHandler(GetProjectTemplatesRequest.Type, this.HandleGetProjectTemplatesRequestAsync);

            this.messageHandlers.SetRequestHandler(DebugAdapterMessages.EvaluateRequest.Type, this.HandleEvaluateRequestAsync);

            this.messageHandlers.SetRequestHandler(GetPSSARulesRequest.Type, this.HandleGetPSSARulesRequestAsync);
            this.messageHandlers.SetRequestHandler(SetPSSARulesRequest.Type, this.HandleSetPSSARulesRequestAsync);

            this.messageHandlers.SetRequestHandler(ScriptRegionRequest.Type, this.HandleGetFormatScriptRegionRequestAsync);

            this.messageHandlers.SetRequestHandler(GetPSHostProcessesRequest.Type, this.HandleGetPSHostProcessesRequestAsync);
            this.messageHandlers.SetRequestHandler(CommentHelpRequest.Type, this.HandleCommentHelpRequestAsync);

            this.messageHandlers.SetRequestHandler(GetRunspaceRequest.Type, this.HandleGetRunspaceRequestAsync);

            // Initialize the extension service
            // TODO: This should be made awaited once Initialize is async!
            this.editorSession.ExtensionService.InitializeAsync(
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

        private async Task HandleShutdownRequestAsync(
            RequestContext<object> requestContext)
        {
            // Allow the implementor to shut down gracefully

            await requestContext.SendResultAsync(new object());
        }

        private async Task HandleExitNotificationAsync(
            object exitParams,
            EventContext eventContext)
        {
            // Stop the server channel
            await this.Stop();
        }

        private Task HandleInitializedNotificationAsync(InitializedParams initializedParams,
            EventContext eventContext)
        {
            // Can do dynamic registration of capabilities in this notification handler
            return Task.FromResult(true);
        }

        protected async Task HandleInitializeRequestAsync(
            InitializeParams initializeParams,
            RequestContext<InitializeResult> requestContext)
        {
            // Grab the workspace path from the parameters
            editorSession.Workspace.WorkspacePath = initializeParams.RootPath;

            // Set the working directory of the PowerShell session to the workspace path
            if (editorSession.Workspace.WorkspacePath != null)
            {
                await editorSession.PowerShellContext.SetWorkingDirectoryAsync(
                    editorSession.Workspace.WorkspacePath,
                    isPathAlreadyEscaped: false);
            }

            await requestContext.SendResultAsync(
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
                        DocumentFormattingProvider = false,
                        DocumentRangeFormattingProvider = false,
                        RenameProvider = false,
                        FoldingRangeProvider = true
                    }
                });
        }

        protected async Task HandleShowHelpRequestAsync(
            string helpParams,
            RequestContext<object> requestContext)
        {
            const string CheckHelpScript = @"
                [CmdletBinding()]
                param (
                    [String]$CommandName
                )
                try {
                    $command = Microsoft.PowerShell.Core\Get-Command $CommandName -ErrorAction Stop
                } catch [System.Management.Automation.CommandNotFoundException] {
                    $PSCmdlet.ThrowTerminatingError($PSItem)
                }
                try {
                    $helpUri = [Microsoft.PowerShell.Commands.GetHelpCodeMethods]::GetHelpUri($command)

                    $oldSslVersion = [System.Net.ServicePointManager]::SecurityProtocol
                    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

                    # HEAD means we don't need the content itself back, just the response header
                    $status = (Microsoft.PowerShell.Utility\Invoke-WebRequest -Method Head -Uri $helpUri -TimeoutSec 5 -ErrorAction Stop).StatusCode
                    if ($status -lt 400) {
                        $null = Microsoft.PowerShell.Core\Get-Help $CommandName -Online
                        return
                    }
                } catch {
                    # Ignore - we want to drop out to Get-Help -Full
                } finally {
                    [System.Net.ServicePointManager]::SecurityProtocol = $oldSslVersion
                }

                return Microsoft.PowerShell.Core\Get-Help $CommandName -Full
                ";

            if (string.IsNullOrEmpty(helpParams)) { helpParams = "Get-Help"; }

            PSCommand checkHelpPSCommand = new PSCommand()
                .AddScript(CheckHelpScript, useLocalScope: true)
                .AddArgument(helpParams);

            // TODO: Rather than print the help in the console, we should send the string back
            //       to VSCode to display in a help pop-up (or similar)
            await editorSession.PowerShellContext.ExecuteCommandAsync<PSObject>(checkHelpPSCommand, sendOutputToHost: true);
            await requestContext.SendResultAsync(null);
        }

        private async Task HandleSetPSSARulesRequestAsync(
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

            var sendresult = requestContext.SendResultAsync(null);
            var scripFile = editorSession.Workspace.GetFile((string)dynParams.filepath);
            await RunScriptDiagnosticsAsync(
                    new ScriptFile[] { scripFile },
                        editorSession,
                        this.messageSender.SendEventAsync);
            await sendresult;
        }

        private async Task HandleGetFormatScriptRegionRequestAsync(
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

            await requestContext.SendResultAsync(new ScriptRegionRequestResult
            {
                scriptRegion = scriptRegion
            });
        }

        private async Task HandleGetPSSARulesRequestAsync(
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

            await requestContext.SendResultAsync(rules);
        }

        private async Task HandleInstallModuleRequestAsync(
            string moduleName,
            RequestContext<object> requestContext
        )
        {
            var script = string.Format("Install-Module -Name {0} -Scope CurrentUser", moduleName);

            var executeTask =
               editorSession.PowerShellContext.ExecuteScriptStringAsync(
                   script,
                   true,
                   true).ConfigureAwait(false);

            await requestContext.SendResultAsync(null);
        }

        private Task HandleInvokeExtensionCommandRequestAsync(
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
                this.editorSession.ExtensionService.InvokeCommandAsync(
                    commandDetails.Name,
                    editorContext);

            commandTask.ContinueWith(t =>
            {
                return requestContext.SendResultAsync(null);
            });

            return Task.FromResult(true);
        }

        private Task HandleNewProjectFromTemplateRequestAsync(
            NewProjectFromTemplateRequest newProjectArgs,
            RequestContext<NewProjectFromTemplateResponse> requestContext)
        {
            // Don't await the Task here so that we don't block the session
            this.editorSession.TemplateService
                .CreateFromTemplateAsync(newProjectArgs.TemplatePath, newProjectArgs.DestinationPath)
                .ContinueWith(
                    async task =>
                    {
                        await requestContext.SendResultAsync(
                            new NewProjectFromTemplateResponse
                            {
                                CreationSuccessful = task.Result
                            });
                    });

            return Task.FromResult(true);
        }

        private async Task HandleGetProjectTemplatesRequestAsync(
            GetProjectTemplatesRequest requestArgs,
            RequestContext<GetProjectTemplatesResponse> requestContext)
        {
            bool plasterInstalled = await this.editorSession.TemplateService.ImportPlasterIfInstalledAsync();

            if (plasterInstalled)
            {
                var availableTemplates =
                    await this.editorSession.TemplateService.GetAvailableTemplatesAsync(
                        requestArgs.IncludeInstalledModules);

                await requestContext.SendResultAsync(
                    new GetProjectTemplatesResponse
                    {
                        Templates = availableTemplates
                    });
            }
            else
            {
                await requestContext.SendResultAsync(
                    new GetProjectTemplatesResponse
                    {
                        NeedsModuleInstall = true,
                        Templates = new TemplateDetails[0]
                    });
            }
        }

        private async Task HandleExpandAliasRequestAsync(
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
            await this.editorSession.PowerShellContext.ExecuteCommandAsync<PSObject>(psCommand);

            psCommand = new PSCommand();
            psCommand.AddCommand("__Expand-Alias").AddArgument(content);
            var result = await this.editorSession.PowerShellContext.ExecuteCommandAsync<string>(psCommand);

            await requestContext.SendResultAsync(result.First().ToString());
        }

        private async Task HandleGetCommandRequestAsync(
            string param,
            RequestContext<object> requestContext)
        {
            PSCommand psCommand = new PSCommand();
            if (!string.IsNullOrEmpty(param))
            {
                psCommand.AddCommand("Microsoft.PowerShell.Core\\Get-Command").AddArgument(param);
            }
            else
            {
                // Executes the following:
                // Get-Command -CommandType Function,Cmdlet,ExternalScript | Select-Object -Property Name,ModuleName | Sort-Object -Property Name
                psCommand
                    .AddCommand("Microsoft.PowerShell.Core\\Get-Command")
                        .AddParameter("CommandType", new[]{"Function", "Cmdlet", "ExternalScript"})
                    .AddCommand("Microsoft.PowerShell.Utility\\Select-Object")
                        .AddParameter("Property", new[]{"Name", "ModuleName"})
                    .AddCommand("Microsoft.PowerShell.Utility\\Sort-Object")
                        .AddParameter("Property", "Name");
            }

            IEnumerable<PSObject> result = await this.editorSession.PowerShellContext.ExecuteCommandAsync<PSObject>(psCommand);

            var commandList = new List<PSCommandMessage>();
            if (result != null)
            {
                foreach (dynamic command in result)
                {
                    commandList.Add(new PSCommandMessage
                    {
                        Name = command.Name,
                        ModuleName = command.ModuleName,
                        Parameters = command.Parameters,
                        ParameterSets = command.ParameterSets,
                        DefaultParameterSet = command.DefaultParameterSet
                    });
                }
            }

            await requestContext.SendResultAsync(commandList);
        }

        private async Task HandleFindModuleRequestAsync(
            object param,
            RequestContext<object> requestContext)
        {
            var psCommand = new PSCommand();
            psCommand.AddScript("Find-Module | Select Name, Description");

            var modules = await editorSession.PowerShellContext.ExecuteCommandAsync<PSObject>(psCommand);

            var moduleList = new List<PSModuleMessage>();

            if (modules != null)
            {
                foreach (dynamic m in modules)
                {
                    moduleList.Add(new PSModuleMessage { Name = m.Name, Description = m.Description });
                }
            }

            await requestContext.SendResultAsync(moduleList);
        }

        protected Task HandleDidOpenTextDocumentNotificationAsync(
            DidOpenTextDocumentParams openParams,
            EventContext eventContext)
        {
            ScriptFile openedFile =
                editorSession.Workspace.GetFileBuffer(
                    openParams.TextDocument.Uri,
                    openParams.TextDocument.Text);

            // TODO: Get all recently edited files in the workspace
            this.RunScriptDiagnosticsAsync(
                new ScriptFile[] { openedFile },
                editorSession,
                eventContext);

            Logger.Write(LogLevel.Verbose, "Finished opening document.");

            return Task.FromResult(true);
        }

        protected async Task HandleDidCloseTextDocumentNotificationAsync(
            DidCloseTextDocumentParams closeParams,
            EventContext eventContext)
        {
            // Find and close the file in the current session
            var fileToClose = editorSession.Workspace.GetFile(closeParams.TextDocument.Uri);

            if (fileToClose != null)
            {
                editorSession.Workspace.CloseFile(fileToClose);
                await ClearMarkersAsync(fileToClose, eventContext);
            }

            Logger.Write(LogLevel.Verbose, "Finished closing document.");
        }
        protected async Task HandleDidSaveTextDocumentNotificationAsync(
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
                    await this.editorSession.RemoteFileManager.SaveRemoteFileAsync(
                        savedFile.FilePath);
                }
            }
        }

        protected Task HandleDidChangeTextDocumentNotificationAsync(
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
            this.RunScriptDiagnosticsAsync(
                changedFiles.ToArray(),
                editorSession,
                eventContext);

            return Task.FromResult(true);
        }

        protected async Task HandleDidChangeConfigurationNotificationAsync(
            DidChangeConfigurationParams<LanguageServerSettingsWrapper> configChangeParams,
            EventContext eventContext)
        {
            bool oldLoadProfiles = this.currentSettings.EnableProfileLoading;
            bool oldScriptAnalysisEnabled =
                this.currentSettings.ScriptAnalysis.Enable.HasValue ? this.currentSettings.ScriptAnalysis.Enable.Value : false;
            string oldScriptAnalysisSettingsPath =
                this.currentSettings.ScriptAnalysis?.SettingsPath;

            this.currentSettings.Update(
                configChangeParams.Settings.Powershell,
                this.editorSession.Workspace.WorkspacePath,
                this.Logger);

            if (!this.profilesLoaded &&
                this.currentSettings.EnableProfileLoading &&
                oldLoadProfiles != this.currentSettings.EnableProfileLoading)
            {
                await this.editorSession.PowerShellContext.LoadHostProfilesAsync();
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
                if (this.editorSession.AnalysisService != null)
                {
                    this.editorSession.AnalysisService.SettingsPath = newSettingsPath;
                    settingsPathChanged = true;
                }
            }

            // If script analysis settings have changed we need to clear & possibly update the current diagnostic records.
            if ((oldScriptAnalysisEnabled != this.currentSettings.ScriptAnalysis?.Enable) || settingsPathChanged)
            {
                // If the user just turned off script analysis or changed the settings path, send a diagnostics
                // event to clear the analysis markers that they already have.
                if (!this.currentSettings.ScriptAnalysis.Enable.Value || settingsPathChanged)
                {
                    foreach (var scriptFile in editorSession.Workspace.GetOpenedFiles())
                    {
                        await ClearMarkersAsync(scriptFile, eventContext);
                    }
                }

                await this.RunScriptDiagnosticsAsync(
                    this.editorSession.Workspace.GetOpenedFiles(),
                    this.editorSession,
                    eventContext);
            }
        }

        protected async Task HandleDefinitionRequestAsync(
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
                    await editorSession.LanguageService.GetDefinitionOfSymbolAsync(
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

            await requestContext.SendResultAsync(definitionLocations.ToArray());
        }

        protected async Task HandleReferencesRequestAsync(
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
                await editorSession.LanguageService.FindReferencesOfSymbolAsync(
                    foundSymbol,
                    editorSession.Workspace.ExpandScriptReferences(scriptFile),
                    editorSession.Workspace);

            Location[] referenceLocations = s_emptyLocationResult;

            if (referencesResult != null)
            {
                var locations = new List<Location>();
                foreach (SymbolReference foundReference in referencesResult.FoundReferences)
                {
                    locations.Add(new Location
                        {
                            Uri = GetFileUri(foundReference.FilePath),
                            Range = GetRangeFromScriptRegion(foundReference.ScriptRegion)
                        });
                }
                referenceLocations = locations.ToArray();
            }

            await requestContext.SendResultAsync(referenceLocations);
        }

        protected async Task HandleCompletionRequestAsync(
            TextDocumentPositionParams textDocumentPositionParams,
            RequestContext<CompletionItem[]> requestContext)
        {
            int cursorLine = textDocumentPositionParams.Position.Line + 1;
            int cursorColumn = textDocumentPositionParams.Position.Character + 1;

            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPositionParams.TextDocument.Uri);

            CompletionResults completionResults =
                await editorSession.LanguageService.GetCompletionsInFileAsync(
                    scriptFile,
                    cursorLine,
                    cursorColumn);

            CompletionItem[] completionItems = s_emptyCompletionResult;

            if (completionResults != null)
            {
                completionItems = new CompletionItem[completionResults.Completions.Length];
                for (int i = 0; i < completionItems.Length; i++)
                {
                    completionItems[i] = CreateCompletionItem(completionResults.Completions[i], completionResults.ReplacedRange, i + 1);
                }
            }

            await requestContext.SendResultAsync(completionItems);
        }

        protected async Task HandleCompletionResolveRequestAsync(
            CompletionItem completionItem,
            RequestContext<CompletionItem> requestContext)
        {
            if (completionItem.Kind == CompletionItemKind.Function)
            {
                // Get the documentation for the function
                CommandInfo commandInfo =
                    await CommandHelpers.GetCommandInfoAsync(
                        completionItem.Label,
                        this.editorSession.PowerShellContext);

                if (commandInfo != null)
                {
                    completionItem.Documentation =
                        await CommandHelpers.GetCommandSynopsisAsync(
                            commandInfo,
                            this.editorSession.PowerShellContext);
                }
            }

            // Send back the updated CompletionItem
            await requestContext.SendResultAsync(completionItem);
        }

        protected async Task HandleSignatureHelpRequestAsync(
            TextDocumentPositionParams textDocumentPositionParams,
            RequestContext<SignatureHelp> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPositionParams.TextDocument.Uri);

            ParameterSetSignatures parameterSets =
                await editorSession.LanguageService.FindParameterSetsInFileAsync(
                    scriptFile,
                    textDocumentPositionParams.Position.Line + 1,
                    textDocumentPositionParams.Position.Character + 1);

            SignatureInformation[] signatures = s_emptySignatureResult;

            if (parameterSets != null)
            {
                signatures = new SignatureInformation[parameterSets.Signatures.Length];
                for (int i = 0; i < signatures.Length; i++)
                {
                    var parameters = new ParameterInformation[parameterSets.Signatures[i].Parameters.Count()];
                    int j = 0;
                    foreach (ParameterInfo param in parameterSets.Signatures[i].Parameters)
                    {
                        parameters[j] = CreateParameterInfo(param);
                        j++;
                    }

                    signatures[i] = new SignatureInformation
                    {
                        Label = parameterSets.CommandName + " " + parameterSets.Signatures[i].SignatureText,
                        Documentation = null,
                        Parameters = parameters,
                    };
                }
            }

            await requestContext.SendResultAsync(
                new SignatureHelp
                {
                    Signatures = signatures,
                    ActiveParameter = null,
                    ActiveSignature = 0
                });
        }

        protected async Task HandleDocumentHighlightRequestAsync(
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

            DocumentHighlight[] documentHighlights = s_emptyHighlightResult;

            if (occurrencesResult != null)
            {
                var highlights = new List<DocumentHighlight>();
                foreach (SymbolReference foundOccurrence in occurrencesResult.FoundOccurrences)
                {
                    highlights.Add(new DocumentHighlight
                    {
                        Kind = DocumentHighlightKind.Write, // TODO: Which symbol types are writable?
                        Range = GetRangeFromScriptRegion(foundOccurrence.ScriptRegion)
                    });
                }
                documentHighlights = highlights.ToArray();
            }

            await requestContext.SendResultAsync(documentHighlights);
        }

        protected async Task HandleHoverRequestAsync(
            TextDocumentPositionParams textDocumentPositionParams,
            RequestContext<Hover> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    textDocumentPositionParams.TextDocument.Uri);

            SymbolDetails symbolDetails =
                await editorSession
                    .LanguageService
                    .FindSymbolDetailsAtLocationAsync(
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

            await requestContext.SendResultAsync(
                new Hover
                {
                    Contents = symbolInfo.ToArray(),
                    Range = symbolRange
                });
        }

        protected async Task HandleDocumentSymbolRequestAsync(
            DocumentSymbolParams documentSymbolParams,
            RequestContext<SymbolInformation[]> requestContext)
        {
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    documentSymbolParams.TextDocument.Uri);

            FindOccurrencesResult foundSymbols =
                editorSession.LanguageService.FindSymbolsInFile(
                    scriptFile);

            string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

            SymbolInformation[] symbols = s_emptySymbolResult;
            if (foundSymbols != null)
            {
                var symbolAcc = new List<SymbolInformation>();
                foreach (SymbolReference foundOccurrence in foundSymbols.FoundOccurrences)
                {
                    var location = new Location
                    {
                        Uri = GetFileUri(foundOccurrence.FilePath),
                        Range = GetRangeFromScriptRegion(foundOccurrence.ScriptRegion)
                    };

                    symbolAcc.Add(new SymbolInformation
                    {
                        ContainerName = containerName,
                        Kind = GetSymbolKind(foundOccurrence.SymbolType),
                        Location = location,
                        Name = GetDecoratedSymbolName(foundOccurrence)
                    });
                }
                symbols = symbolAcc.ToArray();
            }

            await requestContext.SendResultAsync(symbols);
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

        protected async Task HandleWorkspaceSymbolRequestAsync(
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
                    foreach (SymbolReference foundOccurrence in foundSymbols.FoundOccurrences)
                    {
                        if (!IsQueryMatch(workspaceSymbolParams.Query, foundOccurrence.SymbolName))
                        {
                            continue;
                        }

                        var location = new Location
                        {
                            Uri = GetFileUri(foundOccurrence.FilePath),
                            Range = GetRangeFromScriptRegion(foundOccurrence.ScriptRegion)
                        };

                        symbols.Add(new SymbolInformation
                        {
                            ContainerName = containerName,
                            Kind = foundOccurrence.SymbolType == SymbolType.Variable ? SymbolKind.Variable : SymbolKind.Function,
                            Location = location,
                            Name = GetDecoratedSymbolName(foundOccurrence)
                        });
                    }
                }
            }

            await requestContext.SendResultAsync(symbols.ToArray());
        }

        protected async Task HandlePowerShellVersionRequestAsync(
            object noParams,
            RequestContext<PowerShellVersion> requestContext)
        {
            await requestContext.SendResultAsync(
                new PowerShellVersion(
                    this.editorSession.PowerShellContext.LocalPowerShellVersion));
        }

        protected async Task HandleGetPSHostProcessesRequestAsync(
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

                var processes = await editorSession.PowerShellContext.ExecuteCommandAsync<PSObject>(psCommand);
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

            await requestContext.SendResultAsync(psHostProcesses.ToArray());
        }

        protected async Task HandleCommentHelpRequestAsync(
           CommentHelpRequestParams requestParams,
           RequestContext<CommentHelpRequestResult> requestContext)
        {
            var result = new CommentHelpRequestResult();

            ScriptFile scriptFile;
            if (!this.editorSession.Workspace.TryGetFile(requestParams.DocumentUri, out scriptFile))
            {
                await requestContext.SendResultAsync(result);
                return;
            }

            int triggerLine = requestParams.TriggerPosition.Line + 1;

            string helpLocation;
            FunctionDefinitionAst functionDefinitionAst = editorSession.LanguageService.GetFunctionDefinitionForHelpComment(
                scriptFile,
                triggerLine,
                out helpLocation);

            if (functionDefinitionAst == null)
            {
                await requestContext.SendResultAsync(result);
                return;
            }

            IScriptExtent funcExtent = functionDefinitionAst.Extent;
            string funcText = funcExtent.Text;
            if (helpLocation.Equals("begin"))
            {
                // check if the previous character is `<` because it invalidates
                // the param block the follows it.
                IList<string> lines = ScriptFile.GetLines(funcText);
                int relativeTriggerLine0b = triggerLine - funcExtent.StartLineNumber;
                if (relativeTriggerLine0b > 0 && lines[relativeTriggerLine0b].IndexOf("<") > -1)
                {
                    lines[relativeTriggerLine0b] = string.Empty;
                }

                funcText = string.Join("\n", lines);
            }

            ScriptFileMarker[] analysisResults = await this.editorSession.AnalysisService.GetSemanticMarkersAsync(
                funcText,
                AnalysisService.GetCommentHelpRuleSettings(
                    enable: true,
                    exportedOnly: false,
                    blockComment: requestParams.BlockComment,
                    vscodeSnippetCorrection: true,
                    placement: helpLocation));

            string helpText = analysisResults?.FirstOrDefault()?.Correction?.Edits[0].Text;

            if (helpText == null)
            {
                await requestContext.SendResultAsync(result);
                return;
            }

            result.Content = ScriptFile.GetLines(helpText).ToArray();

            if (helpLocation != null &&
                !helpLocation.Equals("before", StringComparison.OrdinalIgnoreCase))
            {
                // we need to trim the leading `{` and newline when helpLocation=="begin"
                // we also need to trim the leading newline when helpLocation=="end"
                result.Content = result.Content.Skip(1).ToArray();
            }

            await requestContext.SendResultAsync(result);
        }

        protected async Task HandleGetRunspaceRequestAsync(
            object noParams,
            RequestContext<GetRunspaceResponse[]> requestContext)
        {
            var runspaceResponses = new List<GetRunspaceResponse>();

            if (this.editorSession.PowerShellContext.LocalPowerShellVersion.Version.Major >= 5)
            {
                var psCommand = new PSCommand();
                psCommand.AddCommand("Get-Runspace");

                IEnumerable<Runspace> runspaces = await editorSession.PowerShellContext.ExecuteCommandAsync<Runspace>(psCommand);
                if (runspaces != null)
                {
                    foreach (var p in runspaces)
                    {
                        runspaceResponses.Add(
                            new GetRunspaceResponse
                            {
                                Id = p.Id,
                                Name = p.Name,
                                Availability = p.RunspaceAvailability.ToString()
                            });
                    }
                }
            }

            await requestContext.SendResultAsync(runspaceResponses.ToArray());
        }

        private bool IsQueryMatch(string query, string symbolName)
        {
            return symbolName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // https://microsoft.github.io/language-server-protocol/specification#textDocument_codeAction
        protected async Task HandleCodeActionRequestAsync(
            CodeActionParams codeActionParams,
            RequestContext<CodeActionCommand[]> requestContext)
        {
            MarkerCorrection correction = null;
            Dictionary<string, MarkerCorrection> markerIndex = null;
            List<CodeActionCommand> codeActionCommands = new List<CodeActionCommand>();

            // If there are any code fixes, send these commands first so they appear at top of "Code Fix" menu in the client UI.
            if (this.codeActionsPerFile.TryGetValue(codeActionParams.TextDocument.Uri, out markerIndex))
            {
                foreach (var diagnostic in codeActionParams.Context.Diagnostics)
                {
                    if (string.IsNullOrEmpty(diagnostic.Code))
                    {
                        this.Logger.Write(
                            LogLevel.Warning,
                            $"textDocument/codeAction skipping diagnostic with empty Code field: {diagnostic.Source} {diagnostic.Message}");

                        continue;
                    }

                    string diagnosticId = GetUniqueIdFromDiagnostic(diagnostic);
                    if (markerIndex.TryGetValue(diagnosticId, out correction))
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

            // Add "show documentation" commands last so they appear at the bottom of the client UI.
            // These commands do not require code fixes. Sometimes we get a batch of diagnostics
            // to create commands for. No need to create multiple show doc commands for the same rule.
            var ruleNamesProcessed = new HashSet<string>();
            foreach (var diagnostic in codeActionParams.Context.Diagnostics)
            {
                if (string.IsNullOrEmpty(diagnostic.Code)) { continue; }

                if (string.Equals(diagnostic.Source, "PSScriptAnalyzer", StringComparison.OrdinalIgnoreCase) &&
                    !ruleNamesProcessed.Contains(diagnostic.Code))
                {
                    ruleNamesProcessed.Add(diagnostic.Code);

                    codeActionCommands.Add(
                        new CodeActionCommand
                        {
                            Title = $"Show documentation for \"{diagnostic.Code}\"",
                            Command = "PowerShell.ShowCodeActionDocumentation",
                            Arguments = JArray.FromObject(new[] { diagnostic.Code })
                        });
                }
            }

            await requestContext.SendResultAsync(
                codeActionCommands.ToArray());
        }

        protected async Task HandleDocumentFormattingRequestAsync(
            DocumentFormattingParams formattingParams,
            RequestContext<TextEdit[]> requestContext)
        {
            var result = await FormatAsync(
                formattingParams.TextDocument.Uri,
                formattingParams.options,
                null);

            await requestContext.SendResultAsync(new TextEdit[1]
            {
                new TextEdit
                {
                    NewText = result.Item1,
                    Range = result.Item2
                },
            });
        }

        protected async Task HandleDocumentRangeFormattingRequestAsync(
            DocumentRangeFormattingParams formattingParams,
            RequestContext<TextEdit[]> requestContext)
        {
            var result = await FormatAsync(
                formattingParams.TextDocument.Uri,
                formattingParams.Options,
                formattingParams.Range);

            await requestContext.SendResultAsync(new TextEdit[1]
            {
                new TextEdit
                {
                    NewText = result.Item1,
                    Range = result.Item2
                },
            });
        }

        protected async Task HandleFoldingRangeRequestAsync(
            FoldingRangeParams foldingParams,
            RequestContext<FoldingRange[]> requestContext)
        {
            await requestContext.SendResultAsync(Fold(foldingParams.TextDocument.Uri));
        }

        protected Task HandleEvaluateRequestAsync(
            DebugAdapterMessages.EvaluateRequestArguments evaluateParams,
            RequestContext<DebugAdapterMessages.EvaluateResponseBody> requestContext)
        {
            // We don't await the result of the execution here because we want
            // to be able to receive further messages while the current script
            // is executing.  This important in cases where the pipeline thread
            // gets blocked by something in the script like a prompt to the user.
            var executeTask =
                this.editorSession.PowerShellContext.ExecuteScriptStringAsync(
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
                        requestContext.SendResultAsync(
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

        private FoldingRange[] Fold(string documentUri)
        {
            // TODO Should be using dynamic registrations
            if (!this.currentSettings.CodeFolding.Enable) { return null; }

            // Avoid crash when using untitled: scheme or any other scheme where the document doesn't
            // have a backing file.  https://github.com/PowerShell/vscode-powershell/issues/1676
            // Perhaps a better option would be to parse the contents of the document as a string
            // as opposed to reading a file but the senario of "no backing file" probably doesn't
            // warrant the extra effort.
            ScriptFile scriptFile;
            if (!editorSession.Workspace.TryGetFile(documentUri, out scriptFile)) { return null; }

            var result = new List<FoldingRange>();

            // If we're showing the last line, decrement the Endline of all regions by one.
            int endLineOffset = this.currentSettings.CodeFolding.ShowLastLine ? -1 : 0;

            foreach (FoldingReference fold in TokenOperations.FoldableReferences(scriptFile.ScriptTokens).References)
            {
                result.Add(new FoldingRange {
                    EndCharacter   = fold.EndCharacter,
                    EndLine        = fold.EndLine + endLineOffset,
                    Kind           = fold.Kind,
                    StartCharacter = fold.StartCharacter,
                    StartLine      = fold.StartLine
                });
            }

            return result.ToArray();
        }

        private async Task<Tuple<string, Range>> FormatAsync(
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

            formattedScript = await editorSession.AnalysisService.FormatAsync(
                scriptFile.Contents,
                pssaSettings,
                rangeList);
            formattedScript = formattedScript ?? scriptFile.Contents;
            return Tuple.Create(formattedScript, editRange);
        }

        private async void PowerShellContext_RunspaceChangedAsync(object sender, Session.RunspaceChangedEventArgs e)
        {
            await this.messageSender.SendEventAsync(
                RunspaceChangedEvent.Type,
                new Protocol.LanguageServer.RunspaceDetails(e.NewRunspace));
        }

        /// <summary>
        /// Event hook on the PowerShell context to listen for changes in script execution status
        /// </summary>
        /// <param name="sender">the PowerShell context sending the execution event</param>
        /// <param name="e">details of the execution status change</param>
        private async void PowerShellContext_ExecutionStatusChangedAsync(object sender, ExecutionStatusChangedEventArgs e)
        {
            await this.messageSender.SendEventAsync(
                ExecutionStatusChangedEvent.Type,
                e);
        }

        private async void ExtensionService_ExtensionAddedAsync(object sender, EditorCommand e)
        {
            await this.messageSender.SendEventAsync(
                ExtensionCommandAddedNotification.Type,
                new ExtensionCommandAddedNotification
                {
                    Name = e.Name,
                    DisplayName = e.DisplayName
                });
        }

        private async void ExtensionService_ExtensionUpdatedAsync(object sender, EditorCommand e)
        {
            await this.messageSender.SendEventAsync(
                ExtensionCommandUpdatedNotification.Type,
                new ExtensionCommandUpdatedNotification
                {
                    Name = e.Name,
                });
        }

        private async void ExtensionService_ExtensionRemovedAsync(object sender, EditorCommand e)
        {
            await this.messageSender.SendEventAsync(
                ExtensionCommandRemovedNotification.Type,
                new ExtensionCommandRemovedNotification
                {
                    Name = e.Name,
                });
        }

        private async void DebugService_DebuggerStoppedAsync(object sender, DebuggerStoppedEventArgs e)
        {
            if (!this.editorSession.DebugService.IsClientAttached)
            {
                await this.messageSender.SendEventAsync(
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
                !filePath.StartsWith("untitled") && !filePath.StartsWith("inmemory")
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

        private Task RunScriptDiagnosticsAsync(
            ScriptFile[] filesToAnalyze,
            EditorSession editorSession,
            EventContext eventContext)
        {
            return RunScriptDiagnosticsAsync(filesToAnalyze, editorSession, this.messageSender.SendEventAsync);
        }

        private Task RunScriptDiagnosticsAsync(
            ScriptFile[] filesToAnalyze,
            EditorSession editorSession,
            Func<NotificationType<PublishDiagnosticsNotification, object>, PublishDiagnosticsNotification, Task> eventSender)
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
                Logger.Write(
                    LogLevel.Error,
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
            Task.Factory.StartNew(
                () =>
                    DelayThenInvokeDiagnosticsAsync(
                        750,
                        filesToAnalyze,
                        this.currentSettings.ScriptAnalysis?.Enable.Value ?? false,
                        this.codeActionsPerFile,
                        editorSession,
                        eventSender,
                        this.Logger,
                        s_existingRequestCancellation.Token),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);

            return Task.FromResult(true);
        }

        private static async Task DelayThenInvokeDiagnosticsAsync(
            int delayMilliseconds,
            ScriptFile[] filesToAnalyze,
            bool isScriptAnalysisEnabled,
            Dictionary<string, Dictionary<string, MarkerCorrection>> correctionIndex,
            EditorSession editorSession,
            EventContext eventContext,
            ILogger Logger,
            CancellationToken cancellationToken)
        {
            await DelayThenInvokeDiagnosticsAsync(
                delayMilliseconds,
                filesToAnalyze,
                isScriptAnalysisEnabled,
                correctionIndex,
                editorSession,
                eventContext.SendEventAsync,
                Logger,
                cancellationToken);
        }


        private static async Task DelayThenInvokeDiagnosticsAsync(
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
                foreach (var script in filesToAnalyze)
                {
                    await PublishScriptDiagnosticsAsync(
                        script,
                        script.SyntaxMarkers,
                        correctionIndex,
                        eventSender);
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
                ScriptFileMarker[] semanticMarkers = null;
                if (isScriptAnalysisEnabled && editorSession.AnalysisService != null)
                {
                    using (Logger.LogExecutionTime($"Script analysis of {scriptFile.FilePath} completed."))
                    {
                        semanticMarkers = await editorSession.AnalysisService.GetSemanticMarkersAsync(scriptFile);
                    }
                }
                else
                {
                    // Semantic markers aren't available if the AnalysisService
                    // isn't available
                    semanticMarkers = new ScriptFileMarker[0];
                }

                await PublishScriptDiagnosticsAsync(
                    scriptFile,
                    // Concat script analysis errors to any existing parse errors
                    scriptFile.SyntaxMarkers.Concat(semanticMarkers).ToArray(),
                    correctionIndex,
                    eventSender);
            }
        }

        private async Task ClearMarkersAsync(ScriptFile scriptFile, EventContext eventContext)
        {
            // send empty diagnostic markers to clear any markers associated with the given file
            await PublishScriptDiagnosticsAsync(
                    scriptFile,
                    new ScriptFileMarker[0],
                    this.codeActionsPerFile,
                    eventContext);
        }

        private static async Task PublishScriptDiagnosticsAsync(
            ScriptFile scriptFile,
            ScriptFileMarker[] markers,
            Dictionary<string, Dictionary<string, MarkerCorrection>> correctionIndex,
            EventContext eventContext)
        {
            await PublishScriptDiagnosticsAsync(
                scriptFile,
                markers,
                correctionIndex,
                eventContext.SendEventAsync);
        }

        private static async Task PublishScriptDiagnosticsAsync(
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
                    string diagnosticId = GetUniqueIdFromDiagnostic(markerDiagnostic);
                    fileCorrections.Add(diagnosticId, marker.Correction);
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

        // Generate a unique id that is used as a key to look up the associated code action (code fix) when
        // we receive and process the textDocument/codeAction message.
        private static string GetUniqueIdFromDiagnostic(Diagnostic diagnostic)
        {
            Position start = diagnostic.Range.Start;
            Position end = diagnostic.Range.End;

            var sb = new StringBuilder(256)
            .Append(diagnostic.Source ?? "?")
            .Append("_")
            .Append(diagnostic.Code ?? "?")
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

