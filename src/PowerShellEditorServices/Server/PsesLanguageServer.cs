// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Extension;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.Template;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace Microsoft.PowerShell.EditorServices.Server
{
    /// <summary>
    /// Server runner class for handling LSP messages for Editor Services.
    /// </summary>
    internal class PsesLanguageServer
    {
        internal ILoggerFactory LoggerFactory { get; }

        internal ILanguageServer LanguageServer { get; private set; }

        private readonly LogLevel _minimumLogLevel;
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly HostStartupInfo _hostDetails;
        private readonly TaskCompletionSource<bool> _serverStart;

        private PsesInternalHost _psesHost;

        /// <summary>
        /// Create a new language server instance.
        /// </summary>
        /// <remarks>
        /// This class is only ever instantiated via <see
        /// cref="EditorServicesServerFactory.CreateLanguageServer"/>. It is essentially a
        /// singleton. The factory hides the logger.
        /// </remarks>
        /// <param name="factory">Factory to create loggers with.</param>
        /// <param name="inputStream">Protocol transport input stream.</param>
        /// <param name="outputStream">Protocol transport output stream.</param>
        /// <param name="hostStartupInfo">Host configuration to instantiate the server and services
        /// with.</param>
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
        /// <remarks>
        /// For the services (including the <see cref="PowerShellContextService">
        /// context wrapper around PowerShell itself) see <see
        /// cref="PsesServiceCollectionExtensions.AddPsesLanguageServices"/>.
        /// </remarks>
        /// <returns>A task that completes when the server is ready and listening.</returns>
        public async Task StartAsync()
        {
            LanguageServer = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(options =>
            {
                options
                    .WithInput(_inputStream)
                    .WithOutput(_outputStream)
                    .WithServices(serviceCollection =>
                    {
                        // NOTE: This adds a lot of services!
                        serviceCollection.AddPsesLanguageServices(_hostDetails);
                    })
                    .ConfigureLogging(builder => builder
                        .AddSerilog(Log.Logger) // TODO: Set dispose to true?
                        .AddLanguageProtocolLogging()
                        .SetMinimumLevel(_minimumLogLevel))
                    // TODO: Consider replacing all WithHandler with AddSingleton
                    .WithHandler<PsesWorkspaceSymbolsHandler>()
                    .WithHandler<PsesTextDocumentHandler>()
                    .WithHandler<GetVersionHandler>()
                    .WithHandler<PsesConfigurationHandler>()
                    .WithHandler<PsesFoldingRangeHandler>()
                    .WithHandler<PsesDocumentFormattingHandler>()
                    .WithHandler<PsesDocumentRangeFormattingHandler>()
                    .WithHandler<PsesReferencesHandler>()
                    .WithHandler<PsesDocumentSymbolHandler>()
                    .WithHandler<PsesDocumentHighlightHandler>()
                    .WithHandler<PSHostProcessAndRunspaceHandlers>()
                    .WithHandler<PsesCodeLensHandlers>()
                    .WithHandler<PsesCodeActionHandler>()
                    .WithHandler<InvokeExtensionCommandHandler>()
                    .WithHandler<PsesCompletionHandler>()
                    .WithHandler<PsesHoverHandler>()
                    .WithHandler<PsesSignatureHelpHandler>()
                    .WithHandler<PsesDefinitionHandler>()
                    .WithHandler<TemplateHandlers>()
                    .WithHandler<GetCommentHelpHandler>()
                    .WithHandler<EvaluateHandler>()
                    .WithHandler<GetCommandHandler>()
                    .WithHandler<ShowHelpHandler>()
                    .WithHandler<ExpandAliasHandler>()
                    .WithHandler<PsesSemanticTokensHandler>()
                    // NOTE: The OnInitialize delegate gets run when we first receive the
                    // _Initialize_ request:
                    // https://microsoft.github.io/language-server-protocol/specifications/specification-current/#initialize
                    .OnInitialize(
                        (languageServer, request, _) =>
                        {
                            Log.Logger.Debug("Initializing OmniSharp Language Server");

                            IServiceProvider serviceProvider = languageServer.Services;

                            _psesHost = serviceProvider.GetService<PsesInternalHost>();

                            WorkspaceService workspaceService = serviceProvider.GetService<WorkspaceService>();

                            // Grab the workspace path from the parameters
                            if (request.RootUri != null)
                            {
                                workspaceService.WorkspacePath = request.RootUri.GetFileSystemPath();
                            }
                            else if (request.WorkspaceFolders != null)
                            {
                                // If RootUri isn't set, try to use the first WorkspaceFolder.
                                // TODO: Support multi-workspace.
                                foreach (OmniSharp.Extensions.LanguageServer.Protocol.Models.WorkspaceFolder workspaceFolder in request.WorkspaceFolders)
                                {
                                    workspaceService.WorkspacePath = workspaceFolder.Uri.GetFileSystemPath();
                                    break;
                                }
                            }

                            return Task.CompletedTask;
                        });
            }).ConfigureAwait(false);

            _serverStart.SetResult(true);
        }

        /// <summary>
        /// Get a task that completes when the server is shut down.
        /// </summary>
        /// <returns>A task that completes when the server is shut down.</returns>
        public async Task WaitForShutdown()
        {
            Log.Logger.Debug("Shutting down OmniSharp Language Server");
            await _serverStart.Task.ConfigureAwait(false);
            await LanguageServer.WaitForExit.ConfigureAwait(false);

            // Doing this means we're able to route through any exceptions experienced on the pipeline thread
            _psesHost.TriggerShutdown();
            await _psesHost.Shutdown.ConfigureAwait(false);
        }
    }
}
