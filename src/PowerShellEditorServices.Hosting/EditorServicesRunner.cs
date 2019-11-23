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
        public static EditorServicesRunner Create(EditorServicesConfig config)
        {
            return new EditorServicesRunner(config);
        }

        private readonly EditorServicesConfig _config;

        private readonly EditorServicesServerFactory _serverFactory;

        private bool _alreadySubscribedDebug;

        private EditorServicesRunner(EditorServicesConfig config)
        {
            _config = config;
            _serverFactory = EditorServicesServerFactory.Create(_config.LogPath, (int)_config.LogLevel);
            _alreadySubscribedDebug = false;
        }

        public async Task RunUntilShutdown()
        {
            bool creatingLanguageServer = _config.LanguageServiceTransport != null;
            bool creatingDebugServer = _config.DebugServiceTransport != null;
            bool isTempDebugSession = creatingDebugServer && !creatingLanguageServer;

            // TODO: Validate config here

            // Set up information required to instantiate servers

            ProfilePathConfig profilePaths = GetProfilePaths(_config.ProfilePaths);

            var hostStartupInfo = new HostStartupInfo(
                _config.HostInfo.Name,
                _config.HostInfo.ProfileId,
                _config.HostInfo.Version,
                _config.PSHost,
                profilePaths.AllUsersProfilePath,
                profilePaths.CurrentUserProfilePath,
                _config.FeatureFlags,
                _config.AdditionalModules,
                _config.LogPath,
                (int)_config.LogLevel,
                consoleReplEnabled: _config.ConsoleRepl != ConsoleReplKind.None,
                usesLegacyReadLine: _config.ConsoleRepl == ConsoleReplKind.LegacyReadLine);

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
            return;
        }

        public void Dispose()
        {
        }

        private async Task RunTempDebugSession(HostStartupInfo hostDetails)
        {
            PsesDebugServer debugServer = await CreateDebugServerForTempSession(hostDetails).ConfigureAwait(false);
            await debugServer.StartAsync().ConfigureAwait(false);
            await debugServer.WaitForShutdown().ConfigureAwait(false);
            return;
        }

        private async Task StartDebugServer(Task<PsesDebugServer> debugServerCreation)
        {
            PsesDebugServer debugServer = await debugServerCreation.ConfigureAwait(false);
            if (!_alreadySubscribedDebug)
            {
                _alreadySubscribedDebug = true;
                debugServer.SessionEnded += DebugServer_OnSessionEnded;
            }
            debugServer.StartAsync();
            return;
        }

        private Task RestartDebugServer(PsesDebugServer debugServer)
        {
            Task<PsesDebugServer> debugServerCreation = RecreateDebugServer(debugServer);
            return StartDebugServer(debugServerCreation);
        }

        private async Task<PsesLanguageServer> CreateLanguageServer(HostStartupInfo hostDetails)
        {
            (Stream inStream, Stream outStream) = await _config.LanguageServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return _serverFactory.CreateLanguageServer(inStream, outStream, hostDetails);
        }

        private async Task<PsesDebugServer> CreateDebugServerWithLanguageServer(PsesLanguageServer languageServer)
        {
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return _serverFactory.CreateDebugServerWithLanguageServer(inStream, outStream, languageServer);
        }

        private async Task<PsesDebugServer> RecreateDebugServer(PsesDebugServer debugServer)
        {
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return _serverFactory.RecreateDebugServer(inStream, outStream, debugServer);
        }

        private async Task<PsesDebugServer> CreateDebugServerForTempSession(HostStartupInfo hostDetails)
        {
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return _serverFactory.CreateDebugServerForTempSession(inStream, outStream, hostDetails);
        }

        private ProfilePathConfig GetProfilePaths(ProfilePathConfig profilePathConfig)
        {
            string allUsersPath = null;
            string currentUserPath = null;

            if (profilePathConfig != null)
            {
                allUsersPath = profilePathConfig.AllUsersProfilePath;
                currentUserPath = profilePathConfig.CurrentUserProfilePath;
            }

            if (allUsersPath == null || currentUserPath == null)
            {
                using (var pwsh = PowerShell.Create())
                {
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

            return new ProfilePathConfig(allUsersPath, currentUserPath);
        }

        private void DebugServer_OnSessionEnded(object sender, EventArgs args)
        {
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
