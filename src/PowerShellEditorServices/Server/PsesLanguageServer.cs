// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Extension;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.General;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;

// See EditorServicesServerFactory.cs for the explanation of this alias.
using HostLogger = System.IObservable<(int logLevel, string message)>;

namespace Microsoft.PowerShell.EditorServices.Server
{
    /// <summary>
    /// Server runner class for handling LSP messages for Editor Services.
    /// </summary>
    internal class PsesLanguageServer
    {
        internal HostLogger HostLogger { get; }
        internal ILanguageServer LanguageServer { get; private set; }
        private readonly LogLevel _minimumLogLevel;
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly HostStartupInfo _hostDetails;
        private readonly TaskCompletionSource<bool> _serverStart;
        private PsesInternalHost _psesHost;
        private IDisposable hostLoggerSubscription;

        /// <summary>
        /// Create a new language server instance.
        /// </summary>
        /// <remarks>
        /// This class is only ever instantiated via <see
        /// cref="EditorServicesServerFactory.CreateLanguageServer"/>. It is essentially a
        /// singleton. The factory hides the logger.
        /// </remarks>
        /// <param name="hostLogger">The host logger to hand off for monitoring.</param>
        /// <param name="inputStream">Protocol transport input stream.</param>
        /// <param name="outputStream">Protocol transport output stream.</param>
        /// <param name="hostStartupInfo">Host configuration to instantiate the server and services
        /// with.</param>
        public PsesLanguageServer(
            HostLogger hostLogger,
            Stream inputStream,
            Stream outputStream,
            HostStartupInfo hostStartupInfo)
        {
            HostLogger = hostLogger;
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
                        .ClearProviders()
                        .AddPsesLanguageServerLogging()
                        .SetMinimumLevel(_minimumLogLevel))
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
                    // If PsesCompletionHandler is not marked as serial, then DidChangeTextDocument
                    // notifications will end up cancelling completion. So quickly typing `Get-`
                    // would result in no completions.
                    //
                    // This also lets completion requests interrupt time consuming background tasks
                    // like the references code lens.
                    .WithHandler<PsesCompletionHandler>(
                        new JsonRpcHandlerOptions() { RequestProcessType = RequestProcessType.Serial })
                    .WithHandler<PsesHoverHandler>()
                    .WithHandler<PsesSignatureHelpHandler>()
                    .WithHandler<PsesDefinitionHandler>()
                    .WithHandler<GetCommentHelpHandler>()
                    .WithHandler<EvaluateHandler>()
                    .WithHandler<GetCommandHandler>()
                    .WithHandler<ShowHelpHandler>()
                    .WithHandler<ExpandAliasHandler>()
                    .WithHandler<PsesSemanticTokensHandler>()
                    .WithHandler<DidChangeWatchedFilesHandler>()
                    .WithHandler<PrepareRenameHandler>()
                    .WithHandler<RenameHandler>()
                    // NOTE: The OnInitialize delegate gets run when we first receive the
                    // _Initialize_ request:
                    // https://microsoft.github.io/language-server-protocol/specifications/specification-current/#initialize
                    .OnInitialize(
                        (languageServer, initializeParams, cancellationToken) =>
                        {
                            // Wire up the HostLogger to the LanguageServer's logger once we are initialized, so that any messages still logged to the HostLogger get sent across the LSP channel. There is no more logging to disk at this point.
                            hostLoggerSubscription = HostLogger.Subscribe(new HostLoggerAdapter(
                                languageServer.Services.GetService<ILogger<HostLoggerAdapter>>()
                            ));

                            // Set the workspace path from the parameters.
                            WorkspaceService workspaceService = languageServer.Services.GetService<WorkspaceService>();
                            if (initializeParams.WorkspaceFolders is not null)
                            {
                                workspaceService.WorkspaceFolders.AddRange(initializeParams.WorkspaceFolders);
                            }

                            // Parse initialization options.
                            JObject initializationOptions = initializeParams.InitializationOptions as JObject;
                            HostStartOptions hostStartOptions = new()
                            {
                                // TODO: We need to synchronize our "default" settings as specified
                                // in the VS Code extension's package.json with the actual default
                                // values in this project. For now, this is going to be the most
                                // annoying setting, so we're defaulting this to true.
                                //
                                // NOTE: The keys start with a lowercase because OmniSharp's client
                                // (used for testing) forces it to be that way.
                                LoadProfiles = initializationOptions?.GetValue("enableProfileLoading")?.Value<bool>()
                                    ?? true,
                                // First check the setting, then use the first workspace folder,
                                // finally fall back to CWD.
                                InitialWorkingDirectory = initializationOptions?.GetValue("initialWorkingDirectory")?.Value<string>()
                                    ?? workspaceService.WorkspaceFolders.FirstOrDefault()?.Uri.GetFileSystemPath()
                                    ?? Directory.GetCurrentDirectory(),
                                // If a shell integration script path is provided, that implies the feature is enabled.
                                ShellIntegrationScript = initializationOptions?.GetValue("shellIntegrationScript")?.Value<string>()
                                    ?? "",
                            };

                            workspaceService.InitialWorkingDirectory = hostStartOptions.InitialWorkingDirectory;

                            _psesHost = languageServer.Services.GetService<PsesInternalHost>();
                            return _psesHost.TryStartAsync(hostStartOptions, cancellationToken);
                        }
                    )
                    .OnShutdown(_ => hostLoggerSubscription.Dispose());
            }).ConfigureAwait(false);

            _serverStart.SetResult(true);
        }

        /// <summary>
        /// Get a task that completes when the server is shut down.
        /// </summary>
        /// <returns>A task that completes when the server is shut down.</returns>
        public async Task WaitForShutdown()
        {
            await _serverStart.Task.ConfigureAwait(false);
            await LanguageServer.WaitForExit.ConfigureAwait(false);

            // Doing this means we're able to route through any exceptions experienced on the pipeline thread
            _psesHost.TriggerShutdown();
            await _psesHost.Shutdown.ConfigureAwait(false);
        }
    }
}
