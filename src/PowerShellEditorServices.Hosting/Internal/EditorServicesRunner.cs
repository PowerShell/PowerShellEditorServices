// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Server;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Class to manage the startup of PowerShell Editor Services.
    /// </summary>
    /// <remarks>
    /// This should be called by <see cref="EditorServicesLoader"/> only after Editor Services has
    /// been loaded. It relies on <see cref="EditorServicesServerFactory"/> to indirectly load <see
    /// cref="Microsoft.Extensions.Logging"/> and <see
    /// cref="Microsoft.Extensions.DependencyInjection"/>.
    /// </remarks>
    internal class EditorServicesRunner : IDisposable
    {
        private readonly HostLogger _logger;

        private readonly EditorServicesConfig _config;

        private readonly ISessionFileWriter _sessionFileWriter;

        private readonly EditorServicesServerFactory _serverFactory;

        private readonly IReadOnlyCollection<IDisposable> _loggersToUnsubscribe;

        private bool _alreadySubscribedDebug;

        public EditorServicesRunner(
            HostLogger logger,
            EditorServicesConfig config,
            ISessionFileWriter sessionFileWriter,
            IReadOnlyCollection<IDisposable> loggersToUnsubscribe)
        {
            _logger = logger;
            _config = config;
            _sessionFileWriter = sessionFileWriter;
            // NOTE: This factory helps to isolate `Microsoft.Extensions.Logging/DependencyInjection`.
            _serverFactory = EditorServicesServerFactory.Create(_config.LogPath, (int)_config.LogLevel, logger);
            _alreadySubscribedDebug = false;
            _loggersToUnsubscribe = loggersToUnsubscribe;
        }

        /// <summary>
        /// Start and run Editor Services and then wait for shutdown.
        /// </summary>
        /// <remarks>
        /// TODO: Use "Async" suffix in names of methods that return an awaitable type.
        /// </remarks>
        /// <returns>A task that ends when Editor Services shuts down.</returns>
        public Task RunUntilShutdown()
        {
            // Start Editor Services (see function below)
            Task runAndAwaitShutdown = CreateEditorServicesAndRunUntilShutdown();

            // Now write the session file
            _logger.Log(PsesLogLevel.Diagnostic, "Writing session file");
            _sessionFileWriter.WriteSessionStarted(_config.LanguageServiceTransport, _config.DebugServiceTransport);

            // Finally, wait for Editor Services to shut down
            _logger.Log(PsesLogLevel.Diagnostic, "Waiting on PSES run/shutdown");
            return runAndAwaitShutdown;
        }

        /// <summary>
        /// TODO: This class probably should not be <see cref="IDisposable"/> as the primary
        /// intention of that interface is to provide cleanup of unmanaged resources, which the
        /// logger certainly is not. Nor is this class used with a <see langword="using"/>. It is
        /// only because of the use of <see cref="_serverFactory"/> that this class is also
        /// disposable, and instead that class should be fixed.
        /// </summary>
        public void Dispose() => _serverFactory.Dispose();

        /// <summary>
        /// This is the servers' entry point, e.g. <c>main</c>, as it instantiates, runs and waits
        /// for the LSP and debug servers at the heart of Editor Services. Uses <see
        /// cref="HostStartupInfo"/>.
        /// </summary>
        /// <remarks>
        /// The logical stack of the program is:
        /// <list type="number">
        /// <listheader>
        ///     <term>Symbol</term>
        ///     <description>Description</description>
        /// </listheader>
        /// <item>
        ///     <term><see cref="Commands.StartEditorServicesCommand"/></term>
        ///     <description>
        ///     The StartEditorServicesCommand PSCmdlet, our PowerShell cmdlet written in C# and
        ///     shipped in the module.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="Commands.StartEditorServicesCommand.EndProcessing"/></term>
        ///     <description>
        ///     As a cmdlet, this is the end of its "process" block, and it instantiates <see
        ///     cref="EditorServicesLoader"/>.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="EditorServicesLoader.LoadAndRunEditorServicesAsync"></term>
        ///     <description>
        ///     Loads isolated dependencies then runs and returns the next task.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><see cref="RunUntilShutdown"></term>
        ///     <description>Task which opens a logfile then returns this task.</description>
        /// </item>
        /// <item>
        ///     <term><see cref="CreateEditorServicesAndRunUntilShutdown"></term>
        ///     <description>This task!</description>
        /// </item>
        /// </list>
        /// </remarks>
        /// <returns>A task that ends when Editor Services shuts down.</returns>
        private async Task CreateEditorServicesAndRunUntilShutdown()
        {
            try
            {
                _logger.Log(PsesLogLevel.Diagnostic, "Creating/running editor services");

                bool creatingLanguageServer = _config.LanguageServiceTransport != null;
                bool creatingDebugServer = _config.DebugServiceTransport != null;
                bool isTempDebugSession = creatingDebugServer && !creatingLanguageServer;

                // Set up information required to instantiate servers
                HostStartupInfo hostStartupInfo = CreateHostStartupInfo();

                // If we just want a temp debug session, run that and do nothing else
                if (isTempDebugSession)
                {
                    await RunTempDebugSessionAsync(hostStartupInfo).ConfigureAwait(false);
                    return;
                }

                // We want LSP and maybe debugging
                // To do that we:
                //  - Create the LSP server
                //  - Possibly kick off the debug server creation
                //  - Start the LSP server
                //  - Possibly start the debug server
                //  - Wait for the LSP server to finish

                // Unsubscribe the host logger here so that the integrated console is not polluted with input after the first prompt
                _logger.Log(PsesLogLevel.Verbose, "Starting server, deregistering host logger and registering shutdown listener");
                if (_loggersToUnsubscribe != null)
                {
                    foreach (IDisposable loggerToUnsubscribe in _loggersToUnsubscribe)
                    {
                        loggerToUnsubscribe.Dispose();
                    }
                }

                WriteStartupBanner();

                PsesLanguageServer languageServer = await CreateLanguageServerAsync(hostStartupInfo).ConfigureAwait(false);

                Task<PsesDebugServer> debugServerCreation = null;
                if (creatingDebugServer)
                {
                    debugServerCreation = CreateDebugServerWithLanguageServerAsync(languageServer);
                }

                Task languageServerStart = languageServer.StartAsync();

                Task debugServerStart = null;
                if (creatingDebugServer)
                {
                    // We don't need to wait for this to start, since we instead wait for it to complete later
                    debugServerStart = StartDebugServer(debugServerCreation);
                }

                await languageServerStart.ConfigureAwait(false);
                if (debugServerStart != null)
                {
                    await debugServerStart.ConfigureAwait(false);
                }
                await languageServer.WaitForShutdown().ConfigureAwait(false);
            }
            finally
            {
                // Resubscribe host logger to log shutdown events to the console
                _logger.Subscribe(new PSHostLogger(_config.PSHost.UI));
            }
        }

        private async Task RunTempDebugSessionAsync(HostStartupInfo hostDetails)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Running temp debug session");
            PsesDebugServer debugServer = await CreateDebugServerForTempSessionAsync(hostDetails).ConfigureAwait(false);
            _logger.Log(PsesLogLevel.Verbose, "Debug server created");
            await debugServer.StartAsync().ConfigureAwait(false);
            _logger.Log(PsesLogLevel.Verbose, "Debug server started");
            await debugServer.WaitForShutdown().ConfigureAwait(false);
        }

        private async Task StartDebugServer(Task<PsesDebugServer> debugServerCreation)
        {
            PsesDebugServer debugServer = await debugServerCreation.ConfigureAwait(false);

            // When the debug server shuts down, we want it to automatically restart
            // To do this, we set an event to allow it to create a new debug server as its session ends
            if (!_alreadySubscribedDebug)
            {
                _logger.Log(PsesLogLevel.Diagnostic, "Subscribing debug server for session ended event");
                _alreadySubscribedDebug = true;
                debugServer.SessionEnded += DebugServer_OnSessionEnded;
            }

            _logger.Log(PsesLogLevel.Diagnostic, "Starting debug server");

            await debugServer.StartAsync().ConfigureAwait(false);
        }

        private Task RestartDebugServerAsync(PsesDebugServer debugServer)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Restarting debug server");
            Task<PsesDebugServer> debugServerCreation = RecreateDebugServerAsync(debugServer);
            return StartDebugServer(debugServerCreation);
        }

        private async Task<PsesLanguageServer> CreateLanguageServerAsync(HostStartupInfo hostDetails)
        {
            _logger.Log(PsesLogLevel.Verbose, $"Creating LSP transport with endpoint {_config.LanguageServiceTransport.EndpointDetails}");
            (Stream inStream, Stream outStream) = await _config.LanguageServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            _logger.Log(PsesLogLevel.Diagnostic, "Creating language server");
            return _serverFactory.CreateLanguageServer(inStream, outStream, hostDetails);
        }

        private async Task<PsesDebugServer> CreateDebugServerWithLanguageServerAsync(PsesLanguageServer languageServer)
        {
            _logger.Log(PsesLogLevel.Verbose, $"Creating debug adapter transport with endpoint {_config.DebugServiceTransport.EndpointDetails}");
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            _logger.Log(PsesLogLevel.Diagnostic, "Creating debug adapter");
            return _serverFactory.CreateDebugServerWithLanguageServer(inStream, outStream, languageServer);
        }

        private async Task<PsesDebugServer> RecreateDebugServerAsync(PsesDebugServer debugServer)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Recreating debug adapter transport");
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            _logger.Log(PsesLogLevel.Diagnostic, "Recreating debug adapter");
            return _serverFactory.RecreateDebugServer(inStream, outStream, debugServer);
        }

        private async Task<PsesDebugServer> CreateDebugServerForTempSessionAsync(HostStartupInfo hostDetails)
        {
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return _serverFactory.CreateDebugServerForTempSession(inStream, outStream, hostDetails);
        }

        private HostStartupInfo CreateHostStartupInfo()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Creating startup info object");

            ProfilePathInfo profilePaths = null;
            if (_config.ProfilePaths.AllUsersAllHosts != null
                || _config.ProfilePaths.AllUsersCurrentHost != null
                || _config.ProfilePaths.CurrentUserAllHosts != null
                || _config.ProfilePaths.CurrentUserCurrentHost != null)
            {
                profilePaths = new ProfilePathInfo(
                    _config.ProfilePaths.CurrentUserAllHosts,
                    _config.ProfilePaths.CurrentUserCurrentHost,
                    _config.ProfilePaths.AllUsersAllHosts,
                    _config.ProfilePaths.AllUsersCurrentHost);
            }

            return new HostStartupInfo(
                _config.HostInfo.Name,
                _config.HostInfo.ProfileId,
                _config.HostInfo.Version,
                _config.PSHost,
                profilePaths,
                _config.FeatureFlags,
                _config.AdditionalModules,
                _config.InitialSessionState,
                _config.LogPath,
                (int)_config.LogLevel,
                consoleReplEnabled: _config.ConsoleRepl != ConsoleReplKind.None,
                usesLegacyReadLine: _config.ConsoleRepl == ConsoleReplKind.LegacyReadLine,
                bundledModulePath: _config.BundledModulePath);
        }

        private void WriteStartupBanner()
        {
            if (_config.ConsoleRepl == ConsoleReplKind.None)
            {
                return;
            }

            _config.PSHost.UI.WriteLine(_config.StartupBanner);
        }

        private void DebugServer_OnSessionEnded(object sender, EventArgs args)
        {
            _logger.Log(PsesLogLevel.Verbose, "Debug session ended, restarting debug service...");
            PsesDebugServer oldServer = (PsesDebugServer)sender;
            oldServer.Dispose();
            _alreadySubscribedDebug = false;
            Task.Run(() => RestartDebugServerAsync(oldServer));
        }
    }
}
