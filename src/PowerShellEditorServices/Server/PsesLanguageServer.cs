//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace Microsoft.PowerShell.EditorServices.Server
{
    /// <summary>
    /// Server runner class for handling LSP messages for Editor Services.
    /// </summary>
    internal class PsesLanguageServer
    {
        internal ILoggerFactory LoggerFactory { get; private set; }

        internal ILanguageServer LanguageServer { get; private set; }

        private readonly LogLevel _minimumLogLevel;
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly HostStartupInfo _hostDetails;
        private readonly TaskCompletionSource<bool> _serverStart;

        /// <summary>
        /// Create a new language server instance.
        /// </summary>
        /// <param name="factory">Factory to create loggers with.</param>
        /// <param name="inputStream">Protocol transport input stream.</param>
        /// <param name="outputStream">Protocol transport output stream.</param>
        /// <param name="hostStartupInfo">Host configuration to instantiate the server and services with.</param>
        public PsesLanguageServer(
            ILoggerFactory factory,
            Stream inputStream,
            Stream outputStream,
            HostStartupInfo hostStartupInfo)
        {
            LoggerFactory = factory;
            _minimumLogLevel = (LogLevel)hostStartupInfo.LogLevel;
            _inputStream = inputStream;
            _outputStream = outputStream;
            _hostDetails = hostStartupInfo;
            _serverStart = new TaskCompletionSource<bool>();
        }

        /// <summary>
        /// Start the server listening for input.
        /// </summary>
        /// <returns>A task that completes when the server is ready and listening.</returns>
        public async Task StartAsync()
        {
            LanguageServer = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
            {
                options
                    .WithInput(_inputStream)
                    .WithOutput(_outputStream)
                    .WithServices(serviceCollection => serviceCollection
                        .AddPsesLanguageServices(_hostDetails))
                    .ConfigureLogging(builder => builder
                        .AddSerilog(Log.Logger)
                        .AddLanguageServer(_minimumLogLevel)
                        .SetMinimumLevel(_minimumLogLevel))
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
                            if (request.RootUri != null)
                            {
                                workspaceService.WorkspacePath = workspaceService.ResolveFileUri(request.RootUri).OriginalString;
                            }

                            // Set the working directory of the PowerShell session to the workspace path
                            if (workspaceService.WorkspacePath != null
                                && Directory.Exists(workspaceService.WorkspacePath))
                            {
                                await serviceProvider.GetService<PowerShellContextService>().SetWorkingDirectoryAsync(
                                    workspaceService.WorkspacePath,
                                    isPathAlreadyEscaped: false);
                            }
                        });
            });

            _serverStart.SetResult(true);
        }

        /// <summary>
        /// Get a task that completes when the server is shut down.
        /// </summary>
        /// <returns>A task that completes when the server is shut down.</returns>
        public async Task WaitForShutdown()
        {
            await _serverStart.Task;
            await LanguageServer.WaitForExit;
        }
    }
}
