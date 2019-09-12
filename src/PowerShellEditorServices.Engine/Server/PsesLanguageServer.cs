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
using Microsoft.PowerShell.EditorServices.Engine.Handlers;
using Microsoft.PowerShell.EditorServices.Engine.Hosting;
using Microsoft.PowerShell.EditorServices.Engine.Services;
using Microsoft.PowerShell.EditorServices.Engine.Services.PowerShellContext;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Microsoft.PowerShell.EditorServices.Engine.Server
{
    internal abstract class PsesLanguageServer
    {
        internal ILoggerFactory LoggerFactory { get; private set; }
        internal ILanguageServer LanguageServer { get; private set; }

        private readonly LogLevel _minimumLogLevel;
        private readonly bool _enableConsoleRepl;
        private readonly HashSet<string> _featureFlags;
        private readonly HostDetails _hostDetails;
        private readonly string[] _additionalModules;
        private readonly PSHost _internalHost;
        private readonly ProfilePaths _profilePaths;
        private readonly TaskCompletionSource<bool> _serverStart;

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
            LoggerFactory = factory;
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
            LanguageServer = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
            {
                options.AddDefaultLoggingProvider();
                options.LoggerFactory = LoggerFactory;
                ILogger logger = options.LoggerFactory.CreateLogger("OptionsStartup");
                options.Services = new ServiceCollection()
                    .AddSingleton<WorkspaceService>()
                    .AddSingleton<SymbolsService>()
                    .AddSingleton<ConfigurationService>()
                    .AddSingleton<PowerShellContextService>(
                        (provider) =>
                            PowerShellContextService.Create(
                                LoggerFactory,
                                provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer>(),
                                _profilePaths,
                                _featureFlags,
                                _enableConsoleRepl,
                                _internalHost,
                                _hostDetails,
                                _additionalModules))
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
            await LanguageServer.WaitForExit;
        }

        protected abstract (Stream input, Stream output) GetInputOutputStreams();
    }
}
