using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Server;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace PowerShellEditorServices.Hosting
{
    internal class EditorServicesRunner : IDisposable
    {
        public static EditorServicesRunner Create(HostLogger logger, EditorServicesConfig config, ISessionFileWriter sessionFileWriter)
        {
            return new EditorServicesRunner(logger, config, sessionFileWriter);
        }

        private readonly HostLogger _logger;

        private readonly EditorServicesConfig _config;

        private readonly ISessionFileWriter _sessionFileWriter;

        private readonly EditorServicesServerFactory _serverFactory;

        private bool _alreadySubscribedDebug;

        private EditorServicesRunner(HostLogger logger, EditorServicesConfig config, ISessionFileWriter sessionFileWriter)
        {
            _logger = logger;
            _config = config;
            _sessionFileWriter = sessionFileWriter;
            _serverFactory = EditorServicesServerFactory.Create(_config.LogPath, (int)_config.LogLevel);
            _alreadySubscribedDebug = false;
        }

        public async Task RunUntilShutdown()
        {
            Task runAndAwaitShutdown = CreateEditorServicesAndRunUntilShutdown();

            _logger.Log(PsesLogLevel.Diagnostic, "Writing session file");
            _sessionFileWriter.WriteSessionStarted(_config.LanguageServiceTransport, _config.DebugServiceTransport);

            await runAndAwaitShutdown.ConfigureAwait(false);
        }

        public void Dispose()
        {
        }

        private async Task CreateEditorServicesAndRunUntilShutdown()
        {
            bool creatingLanguageServer = _config.LanguageServiceTransport != null;
            bool creatingDebugServer = _config.DebugServiceTransport != null;
            bool isTempDebugSession = creatingDebugServer && !creatingLanguageServer;

            // TODO: Validate config here

            // Set up information required to instantiate servers
            HostStartupInfo hostStartupInfo = CreateHostStartupInfo();

            if (isTempDebugSession)
            {
                await RunTempDebugSession(hostStartupInfo).ConfigureAwait(false);
                return;
            }

            PsesLanguageServer languageServer = await CreateLanguageServer(hostStartupInfo).ConfigureAwait(false);

            Task<PsesDebugServer> debugServerCreation = null;
            if (creatingDebugServer)
            {
                debugServerCreation = CreateDebugServerWithLanguageServer(languageServer);
            }

            languageServer.StartAsync();

            if (creatingDebugServer)
            {
                StartDebugServer(debugServerCreation);
            }

            await languageServer.WaitForShutdown().ConfigureAwait(false);
        }

        private async Task RunTempDebugSession(HostStartupInfo hostDetails)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Running temp debug session");
            PsesDebugServer debugServer = await CreateDebugServerForTempSession(hostDetails).ConfigureAwait(false);
            _logger.Log(PsesLogLevel.Verbose, "Debug server created");
            await debugServer.StartAsync().ConfigureAwait(false);
            _logger.Log(PsesLogLevel.Verbose, "Debug server started");
            await debugServer.WaitForShutdown().ConfigureAwait(false);
            return;
        }

        private async Task StartDebugServer(Task<PsesDebugServer> debugServerCreation)
        {
            PsesDebugServer debugServer = await debugServerCreation.ConfigureAwait(false);
            if (!_alreadySubscribedDebug)
            {
                _logger.Log(PsesLogLevel.Diagnostic, "Subscribing debug server for session ended event");
                _alreadySubscribedDebug = true;
                debugServer.SessionEnded += DebugServer_OnSessionEnded;
            }
            _logger.Log(PsesLogLevel.Diagnostic, "Starting debug server");
            debugServer.StartAsync();
            return;
        }

        private Task RestartDebugServer(PsesDebugServer debugServer)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Restarting debug server");
            Task<PsesDebugServer> debugServerCreation = RecreateDebugServer(debugServer);
            return StartDebugServer(debugServerCreation);
        }

        private async Task<PsesLanguageServer> CreateLanguageServer(HostStartupInfo hostDetails)
        {
            _logger.Log(PsesLogLevel.Verbose, $"Creating LSP transport with endpoint {_config.LanguageServiceTransport.Endpoint}");
            (Stream inStream, Stream outStream) = await _config.LanguageServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            _logger.Log(PsesLogLevel.Diagnostic, "Creating language server");
            return _serverFactory.CreateLanguageServer(inStream, outStream, hostDetails);
        }

        private async Task<PsesDebugServer> CreateDebugServerWithLanguageServer(PsesLanguageServer languageServer)
        {
            _logger.Log(PsesLogLevel.Verbose, $"Creating debug adapter transport with endpoint {_config.DebugServiceTransport.Endpoint}");
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            _logger.Log(PsesLogLevel.Diagnostic, "Creating debug adapter");
            return _serverFactory.CreateDebugServerWithLanguageServer(inStream, outStream, languageServer);
        }

        private async Task<PsesDebugServer> RecreateDebugServer(PsesDebugServer debugServer)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Recreating debug adapter transport");
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            _logger.Log(PsesLogLevel.Diagnostic, "Recreating debug adapter");
            return _serverFactory.RecreateDebugServer(inStream, outStream, debugServer);
        }

        private async Task<PsesDebugServer> CreateDebugServerForTempSession(HostStartupInfo hostDetails)
        {
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return _serverFactory.CreateDebugServerForTempSession(inStream, outStream, hostDetails);
        }

        private HostStartupInfo CreateHostStartupInfo()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Creating startup info object");

            (string allUsersProfilePath, string currentUserProfilePath) = GetProfilePaths(_config.ProfilePaths?.AllUsersProfilePath, _config.ProfilePaths?.CurrentUserProfilePath);

            return new HostStartupInfo(
                _config.HostInfo.Name,
                _config.HostInfo.ProfileId,
                _config.HostInfo.Version,
                _config.PSHost,
                allUsersProfilePath,
                currentUserProfilePath,
                _config.FeatureFlags,
                _config.AdditionalModules,
                _config.LogPath,
                (int)_config.LogLevel,
                consoleReplEnabled: _config.ConsoleRepl != ConsoleReplKind.None,
                usesLegacyReadLine: _config.ConsoleRepl == ConsoleReplKind.LegacyReadLine);
        }

        private (string allUsersPath, string currentUserPath) GetProfilePaths(string allUsersPath, string currentUserPath)
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Configuring profile paths");

            if (allUsersPath == null || currentUserPath == null)
            {
                using (var pwsh = PowerShell.Create())
                {
                    _logger.Log(PsesLogLevel.Diagnostic, "Querying PowerShell for profile paths");
                    Collection<string> profiles = pwsh.AddScript("$profile.AllUsersAllHosts,$profile.CurrentUserAllHosts")
                        .Invoke<string>();

                    if (allUsersPath == null)
                    {
                        allUsersPath = profiles[0];
                    }

                    if (currentUserPath == null)
                    {
                        currentUserPath = profiles[1];
                    }
                }
            }

            return (allUsersPath, currentUserPath);
        }

        private void DebugServer_OnSessionEnded(object sender, EventArgs args)
        {
            _logger.Log(PsesLogLevel.Verbose, "Debug session ended. Restarting debug service");
            var oldServer = (PsesDebugServer)sender;
            oldServer.Dispose();
            _alreadySubscribedDebug = false;
            Task.Run(() =>
            {
                RestartDebugServer(oldServer);
            });
        }
    }
}
