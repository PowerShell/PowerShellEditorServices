//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Host;
using Microsoft.PowerShell.EditorServices.Templates;
using Microsoft.PowerShell.EditorServices.TextDocument;
using OmniSharp.Extensions.LanguageServer.Server;
using PowerShellEditorServices.Engine.Services.Handlers;

namespace Microsoft.PowerShell.EditorServices.Engine.Server
{
    internal abstract class PsesLanguageServer
    {
        protected readonly ILoggerFactory _loggerFactory;
        private readonly LogLevel _minimumLogLevel;
        private readonly bool _enableConsoleRepl;
        private readonly HashSet<string> _featureFlags;
        private readonly HostDetails _hostDetails;
        private readonly string[] _additionalModules;
        private readonly PSHost _internalHost;
        private readonly ProfilePaths _profilePaths;
        private readonly TaskCompletionSource<bool> _serverStart;

        private ILanguageServer _languageServer;

        internal PsesLanguageServer(
            ILoggerFactory factory,
            LogLevel minimumLogLevel,
            bool enableConsoleRepl,
            HashSet<string> featureFlags,
            HostDetails hostDetails,
            string[] additionalModules,
            PSHost internalHost,
            ProfilePaths profilePaths)
        {
            _loggerFactory = factory;
            _minimumLogLevel = minimumLogLevel;
            _enableConsoleRepl = enableConsoleRepl;
            _featureFlags = featureFlags;
            _hostDetails = hostDetails;
            _additionalModules = additionalModules;
            _internalHost = internalHost;
            _profilePaths = profilePaths;
            _serverStart = new TaskCompletionSource<bool>();
        }

        public async Task StartAsync()
        {
            _languageServer = await LanguageServer.From(options =>
            {
                options.AddDefaultLoggingProvider();
                options.LoggerFactory = _loggerFactory;
                ILogger logger = options.LoggerFactory.CreateLogger("OptionsStartup");
                options.Services = new ServiceCollection()
                    .AddSingleton<WorkspaceService>()
                    .AddSingleton<SymbolsService>()
                    .AddSingleton<ConfigurationService>()
                    .AddSingleton<PowerShellContextService>(
                        (provider) =>
                            GetFullyInitializedPowerShellContext(
                                provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer>(),
                                _profilePaths))
                    .AddSingleton<TemplateService>()
                    .AddSingleton<EditorOperationsService>()
                    .AddSingleton<ExtensionService>(
                        (provider) =>
                        {
                            var extensionService = new ExtensionService(
                                provider.GetService<PowerShellContextService>(),
                                provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer>());
                            extensionService.InitializeAsync(
                                serviceProvider: provider,
                                editorOperations: provider.GetService<EditorOperationsService>())
                                .Wait();
                            return extensionService;
                        })
                    .AddSingleton<AnalysisService>(
                        (provider) =>
                        {
                            return AnalysisService.Create(
                                provider.GetService<ConfigurationService>(),
                                provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer>(),
                                options.LoggerFactory.CreateLogger<AnalysisService>());
                        });

                (Stream input, Stream output) = GetInputOutputStreams();

                options
                    .WithInput(input)
                    .WithOutput(output);

                options.MinimumLogLevel = _minimumLogLevel;

                logger.LogInformation("Adding handlers");

                options
                    .WithHandler<WorkspaceSymbolsHandler>()
                    .WithHandler<TextDocumentHandler>()
                    .WithHandler<GetVersionHandler>()
                    .WithHandler<ConfigurationHandler>()
                    .WithHandler<FoldingRangeHandler>()
                    .WithHandler<DocumentFormattingHandler>()
                    .WithHandler<DocumentRangeFormattingHandler>()
                    .WithHandler<ReferencesHandler>()
                    .WithHandler<DocumentSymbolHandler>()
                    .WithHandler<DocumentHighlightHandler>()
                    .WithHandler<PSHostProcessAndRunspaceHandlers>()
                    .WithHandler<CodeLensHandlers>()
                    .WithHandler<CodeActionHandler>()
                    .WithHandler<InvokeExtensionCommandHandler>()
                    .WithHandler<CompletionHandler>()
                    .WithHandler<HoverHandler>()
                    .WithHandler<SignatureHelpHandler>()
                    .WithHandler<DefinitionHandler>()
                    .WithHandler<TemplateHandlers>()
                    .WithHandler<GetCommentHelpHandler>()
                    .WithHandler<EvaluateHandler>()
                    .WithHandler<GetCommandHandler>()
                    .WithHandler<ShowHelpHandler>()
                    .WithHandler<ExpandAliasHandler>()
                    .OnInitialize(
                        async (languageServer, request) =>
                        {
                            var serviceProvider = languageServer.Services;
                            var workspaceService = serviceProvider.GetService<WorkspaceService>();

                            // Grab the workspace path from the parameters
                            workspaceService.WorkspacePath = request.RootPath;

                            // Set the working directory of the PowerShell session to the workspace path
                            if (workspaceService.WorkspacePath != null
                                && Directory.Exists(workspaceService.WorkspacePath))
                            {
                                await serviceProvider.GetService<PowerShellContextService>().SetWorkingDirectoryAsync(
                                    workspaceService.WorkspacePath,
                                    isPathAlreadyEscaped: false);
                            }
                        });

                logger.LogInformation("Handlers added");
            });
        }

        public async Task WaitForShutdown()
        {
            await _serverStart.Task;
            await _languageServer.WaitForExit;
        }

        private PowerShellContextService GetFullyInitializedPowerShellContext(
            OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer languageServer,
            ProfilePaths profilePaths)
        {
            var logger = _loggerFactory.CreateLogger<PowerShellContextService>();

            // PSReadLine can only be used when -EnableConsoleRepl is specified otherwise
            // issues arise when redirecting stdio.
            var powerShellContext = new PowerShellContextService(
                logger,
                languageServer,
                _featureFlags.Contains("PSReadLine") && _enableConsoleRepl);

            EditorServicesPSHostUserInterface hostUserInterface =
                _enableConsoleRepl
                    ? (EditorServicesPSHostUserInterface)new TerminalPSHostUserInterface(powerShellContext, logger, _internalHost)
                    : new ProtocolPSHostUserInterface(languageServer, powerShellContext, logger);

            EditorServicesPSHost psHost =
                new EditorServicesPSHost(
                    powerShellContext,
                    _hostDetails,
                    hostUserInterface,
                    logger);

            Runspace initialRunspace = PowerShellContextService.CreateRunspace(psHost);
            powerShellContext.Initialize(profilePaths, initialRunspace, true, hostUserInterface);

            powerShellContext.ImportCommandsModuleAsync(
                Path.Combine(
                    Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location),
                    @"..\Commands"));

            // TODO: This can be moved to the point after the $psEditor object
            // gets initialized when that is done earlier than LanguageServer.Initialize
            foreach (string module in this._additionalModules)
            {
                var command =
                    new PSCommand()
                        .AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                        .AddParameter("Name", module);

                powerShellContext.ExecuteCommandAsync<PSObject>(
                    command,
                    sendOutputToHost: false,
                    sendErrorToHost: true);
            }

            return powerShellContext;
        }

        protected abstract (Stream input, Stream output) GetInputOutputStreams();
    }
}
