using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Server;
using Serilog;
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
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File(config.LogPath)
                .MinimumLevel.Verbose()
                .CreateLogger();

            return new EditorServicesRunner(new LoggerFactory().AddSerilog(Log.Logger), config);
        }

        private readonly ILoggerFactory _loggerFactory;

        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        private readonly EditorServicesConfig _config;

        private bool _alreadySubscribedDebug;

        private EditorServicesRunner(ILoggerFactory loggerFactory, EditorServicesConfig config)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<EditorServicesRunner>();
            _config = config;
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

            var hostDetails = new HostDetails(
                _config.HostInfo.Name,
                _config.HostInfo.ProfileId,
                _config.HostInfo.Version,
                _config.PSHost,
                profilePaths.AllUsersProfilePath,
                profilePaths.CurrentUserProfilePath,
                consoleReplEnabled: _config.ConsoleRepl != ConsoleReplKind.None,
                usesLegacyReadLine: _config.ConsoleRepl == ConsoleReplKind.LegacyReadLine);

            LogLevel minimumLogLevel = ConvertToExtensionLogLevel(_config.LogLevel);

            if (isTempDebugSession)
            {
                await RunTempDebugSession(hostDetails, minimumLogLevel).ConfigureAwait(false);
                return;
            }

            PsesLanguageServer languageServer = await CreateLanguageServer(hostDetails, minimumLogLevel).ConfigureAwait(false);

            Task<PsesDebugServer> debugServerCreation = null;
            if (creatingDebugServer)
            {
                debugServerCreation = CreateDebugServerWithLanguageServer(languageServer.LanguageServer.Services);
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
            _loggerFactory.Dispose();
        }

        private async Task RunTempDebugSession(HostDetails hostDetails, LogLevel minimumLogLevel)
        {
            PsesDebugServer debugServer = await CreateDebugServerForTempSession(hostDetails, minimumLogLevel).ConfigureAwait(false);
            await debugServer.StartAsync().ConfigureAwait(false);
            await debugServer.WaitForShutdown().ConfigureAwait(false);
            return;
        }

        private Task StartDebugServer(IServiceProvider serviceProvider)
        {
            Task<PsesDebugServer> debugServerCreation = CreateDebugServerWithLanguageServer(serviceProvider);
            return StartDebugServer(debugServerCreation);
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

        private async Task<PsesLanguageServer> CreateLanguageServer(HostDetails hostDetails, LogLevel minimumLogLevel)
        {
            (Stream inStream, Stream outStream) = await _config.LanguageServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return new PsesLanguageServer(
                _loggerFactory,
                minimumLogLevel,
                inStream,
                outStream,
                _config.FeatureFlags ?? Array.Empty<string>(),
                hostDetails,
                _config.AdditionalModules ?? Array.Empty<string>());
        }

        private async Task<PsesDebugServer> CreateDebugServerWithLanguageServer(IServiceProvider languageServerServiceProvider)
        {
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return PsesDebugServer.CreateWithLanguageServerServices(_loggerFactory, inStream, outStream, languageServerServiceProvider);
        }

        private async Task<PsesDebugServer> CreateDebugServerForTempSession(HostDetails hostDetails, LogLevel minimumLogLevel)
        {
            (Stream inStream, Stream outStream) = await _config.DebugServiceTransport.ConnectStreamsAsync().ConfigureAwait(false);

            return PsesDebugServer.CreateForTempSession(_loggerFactory, minimumLogLevel, inStream, outStream, _config.FeatureFlags, hostDetails, _config.AdditionalModules);
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
            IServiceProvider serviceProvider = oldServer.ServiceProvider;
            oldServer.Dispose();
            _alreadySubscribedDebug = false;
            Task.Run(() =>
            {
                StartDebugServer(serviceProvider);
            });
        }

        private static LogLevel ConvertToExtensionLogLevel(PsesLogLevel logLevel)
        {
            switch (logLevel)
            {
                case PsesLogLevel.Diagnostic:
                    return LogLevel.Trace;

                case PsesLogLevel.Verbose:
                    return LogLevel.Debug;

                case PsesLogLevel.Normal:
                    return LogLevel.Information;

                case PsesLogLevel.Warning:
                    return LogLevel.Warning;

                case PsesLogLevel.Error:
                    return LogLevel.Error;

                default:
                    return LogLevel.Information;
            }
        }
    }
}
