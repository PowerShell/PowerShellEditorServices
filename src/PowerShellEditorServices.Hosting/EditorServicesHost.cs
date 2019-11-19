﻿using System;
using System.Collections.Generic;
using System.Management.Automation.Host;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// Provides a simplified interface for hosting the language and debug services
    /// over the named pipe server protocol.
    /// </summary>
    internal class EditorServicesHost
    {
        #region Private Fields

        private readonly HostDetails _hostDetails;

        private readonly PSHost _internalHost;

        private readonly bool _enableConsoleRepl;

        private readonly bool _useLegacyReadLine;

        private readonly HashSet<string> _featureFlags;

        private readonly string[] _additionalModules;

        private PsesLanguageServer _languageServer;

        private PsesDebugServer _debugServer;

        private Microsoft.Extensions.Logging.ILogger _logger;

        private ILoggerFactory _factory;

        #endregion

        #region Properties

        public EditorServicesHostStatus Status { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the EditorServicesHost class and waits for
        /// the debugger to attach if waitForDebugger is true.
        /// </summary>
        /// <param name="hostDetails">The details of the host which is launching PowerShell Editor Services.</param>
        /// <param name="bundledModulesPath">Provides a path to PowerShell modules bundled with the host, if any.  Null otherwise.</param>
        /// <param name="waitForDebugger">If true, causes the host to wait for the debugger to attach before proceeding.</param>
        /// <param name="additionalModules">Modules to be loaded when initializing the new runspace.</param>
        /// <param name="featureFlags">Features to enable for this instance.</param>
        public EditorServicesHost(
            HostDetails hostDetails,
            string bundledModulesPath,
            bool enableConsoleRepl,
            bool useLegacyReadLine,
            bool waitForDebugger,
            string[] additionalModules,
            string[] featureFlags)
            : this(
                hostDetails,
                bundledModulesPath,
                enableConsoleRepl,
                useLegacyReadLine,
                waitForDebugger,
                additionalModules,
                featureFlags,
                GetInternalHostFromDefaultRunspace())
        {
        }

        /// <summary>
        /// Initializes a new instance of the EditorServicesHost class and waits for
        /// the debugger to attach if waitForDebugger is true.
        /// </summary>
        /// <param name="hostDetails">The details of the host which is launching PowerShell Editor Services.</param>
        /// <param name="bundledModulesPath">Provides a path to PowerShell modules bundled with the host, if any.  Null otherwise.</param>
        /// <param name="waitForDebugger">If true, causes the host to wait for the debugger to attach before proceeding.</param>
        /// <param name="additionalModules">Modules to be loaded when initializing the new runspace.</param>
        /// <param name="featureFlags">Features to enable for this instance.</param>
        /// <param name="internalHost">The value of the $Host variable in the original runspace.</param>
        public EditorServicesHost(
            HostDetails hostDetails,
            string bundledModulesPath,
            bool enableConsoleRepl,
            bool useLegacyReadLine,
            bool waitForDebugger,
            string[] additionalModules,
            string[] featureFlags,
            PSHost internalHost)
        {
            // Validate.IsNotNull(nameof(hostDetails), hostDetails);
            // Validate.IsNotNull(nameof(internalHost), internalHost);

            _hostDetails = hostDetails;

            _enableConsoleRepl = enableConsoleRepl;
            _useLegacyReadLine = useLegacyReadLine;
            _additionalModules = additionalModules ?? Array.Empty<string>();
            _featureFlags = new HashSet<string>(featureFlags ?? Array.Empty<string>());
            _internalHost = internalHost;

#if DEBUG
            if (waitForDebugger)
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Break();
                }
                else
                {
                    System.Diagnostics.Debugger.Launch();
                }
            }
#endif

            // Catch unhandled exceptions for logging purposes
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the Logger for the specified file path and log level.
        /// </summary>
        /// <param name="logFilePath">The path of the log file to be written.</param>
        /// <param name="logLevel">The minimum level of log messages to be written.</param>
        public void StartLogging(string logFilePath, PsesLogLevel logLevel)
        {
        }

        /// <summary>
        /// Starts the language service with the specified config.
        /// </summary>
        /// <param name="config">The config that contains information on the communication protocol that will be used.</param>
        /// <param name="profilePaths">The profiles that will be loaded in the session.</param>
        public void StartLanguageService(
            EditorServiceTransportConfig config,
            ProfilePaths profilePaths)
        {
            _logger.LogInformation($"LSP NamedPipe: {config.InOutPipeName}\nLSP OutPipe: {config.OutPipeName}");

            switch (config.TransportType)
            {
                case EditorServiceTransportType.NamedPipe:
                    _languageServer = new NamedPipePsesLanguageServer(
                        _factory,
                        LogLevel.Trace,
                        _enableConsoleRepl,
                        _useLegacyReadLine,
                        _featureFlags,
                        _hostDetails,
                        _additionalModules,
                        _internalHost,
                        profilePaths,
                        config.InOutPipeName ?? config.InPipeName,
                        config.OutPipeName);
                    break;

                case EditorServiceTransportType.Stdio:
                    _languageServer = new StdioPsesLanguageServer(
                        _factory,
                        LogLevel.Trace,
                        _featureFlags,
                        _hostDetails,
                        _additionalModules,
                        _internalHost,
                        profilePaths);
                    break;
            }

            _logger.LogInformation("Starting language server");

            Task.Run(_languageServer.StartAsync);

            _logger.LogInformation(
                string.Format(
                    "Language service started, type = {0}, endpoint = {1}",
                    config.TransportType, config.Endpoint));
        }


        private bool alreadySubscribedDebug;
        /// <summary>
        /// Starts the debug service with the specified config.
        /// </summary>
        /// <param name="config">The config that contains information on the communication protocol that will be used.</param>
        /// <param name="profilePaths">The profiles that will be loaded in the session.</param>
        /// <param name="useTempSession">Determines if we will make a new session typically used for temporary console debugging.</param>
        public void StartDebugService(
            EditorServiceTransportConfig config,
            ProfilePaths profilePaths,
            bool useTempSession)
        {
            _logger.LogInformation($"Debug NamedPipe: {config.InOutPipeName}\nDebug OutPipe: {config.OutPipeName}");

            IServiceProvider serviceProvider = null;
            if (useTempSession)
            {
                serviceProvider = new ServiceCollection()
                    .AddLogging(builder => builder
                        .ClearProviders()
                        .AddSerilog()
                        .SetMinimumLevel(LogLevel.Trace))
                    .AddSingleton<ILanguageServer>(provider => null)
                    .AddPsesLanguageServices(
                        profilePaths,
                        _featureFlags,
                        _enableConsoleRepl,
                        _useLegacyReadLine,
                        _internalHost,
                        _hostDetails,
                        _additionalModules)
                    .BuildServiceProvider();
            }

            switch (config.TransportType)
            {
                case EditorServiceTransportType.NamedPipe:
                    NamedPipeServerStream inNamedPipe = CreateNamedPipe(
                        config.InOutPipeName ?? config.InPipeName,
                        config.OutPipeName,
                        out NamedPipeServerStream outNamedPipe);

                    _debugServer = new PsesDebugServer(
                        _factory,
                        inNamedPipe,
                        outNamedPipe ?? inNamedPipe);

                    Task[] tasks = outNamedPipe != null
                        ? new[] { inNamedPipe.WaitForConnectionAsync(), outNamedPipe.WaitForConnectionAsync() }
                        : new[] { inNamedPipe.WaitForConnectionAsync() };
                    Task.WhenAll(tasks)
                        .ContinueWith(async task =>
                        {
                            _logger.LogInformation("Starting debug server");
                            await _debugServer.StartAsync(serviceProvider ?? _languageServer.LanguageServer.Services, useTempSession);
                            _logger.LogInformation(
                                $"Debug service started, type = {config.TransportType}, endpoint = {config.Endpoint}");
                        });

                    break;

                case EditorServiceTransportType.Stdio:
                    _debugServer = new PsesDebugServer(
                        _factory,
                        Console.OpenStandardInput(),
                        Console.OpenStandardOutput());

                    _logger.LogInformation("Starting debug server");
                    Task.Run(async () =>
                    {

                        await _debugServer.StartAsync(serviceProvider ?? _languageServer.LanguageServer.Services, useTempSession);
                        _logger.LogInformation(
                            $"Debug service started, type = {config.TransportType}, endpoint = {config.Endpoint}");
                    });
                    break;

                default:
                    throw new NotSupportedException($"The transport {config.TransportType} is not supported");
            }

            // If the instance of PSES is being used for debugging only, then we don't want to allow automatic restarting
            // because the user can simply spin up a new PSES if they need to.
            // This design decision was done since this "debug-only PSES" is used in the "Temporary Integrated Console debugging"
            // feature which does not want PSES to be restarted so that the user can see the output of the last debug
            // session.
            if(!alreadySubscribedDebug && !useTempSession)
            {
                alreadySubscribedDebug = true;
                _debugServer.SessionEnded += (sender, eventArgs) =>
                {
                    _debugServer.Dispose();
                    alreadySubscribedDebug = false;
                    StartDebugService(config, profilePaths, useTempSession);
                };
            }
        }

        /// <summary>
        /// Stops the language or debug services if either were started.
        /// </summary>
        public void StopServices()
        {
            // TODO: Need a new way to shut down the services
        }

        /// <summary>
        /// Waits for either the language or debug service to shut down.
        /// </summary>
        public void WaitForCompletion()
        {
            // If _languageServer is not null, then we are either using:
            // Stdio - that only uses a LanguageServer so we return when that has shutdown.
            // NamedPipes - that uses both LanguageServer and DebugServer, but LanguageServer
            //              is the core of PowerShell Editor Services and if that shuts down,
            //              we want the whole process to shutdown.
            if (_languageServer != null)
            {
                _languageServer.WaitForShutdown().GetAwaiter().GetResult();
                return;
            }

            // If there is no LanguageServer, then we must be running with the DebugServiceOnly switch
            // (used in Temporary console debugging) and we need to wait for the DebugServer to shutdown.
            _debugServer.WaitForShutdown().GetAwaiter().GetResult();
        }

        #endregion

        #region Private Methods

        private static PSHost GetInternalHostFromDefaultRunspace()
        {
            using (var pwsh = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                return pwsh.AddScript("$Host").Invoke<PSHost>().First();
            }
        }

        /// <summary>
        /// Gets the OSArchitecture for logging. Cannot use System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
        /// directly, since this tries to load API set DLLs in win7 and crashes.
        /// </summary>
        private string GetOSArchitecture()
        {
            // If on win7 (version 6.1.x), avoid System.Runtime.InteropServices.RuntimeInformation
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version < new Version(6, 2))
            {
                if (Environment.Is64BitProcess)
                {
                    return "X64";
                }

                return "X86";
            }

            return RuntimeInformation.OSArchitecture.ToString();
        }

        private void CurrentDomain_UnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            // Log the exception
            _logger.LogError($"FATAL UNHANDLED EXCEPTION: {e.ExceptionObject}");
        }

        private static NamedPipeServerStream CreateNamedPipe(
            string inOutPipeName,
            string outPipeName,
            out NamedPipeServerStream outPipe)
        {
            // .NET Core implementation is simplest so try that first
            if (VersionUtils.IsNetCore)
            {
                outPipe = outPipeName == null
                    ? null
                    : new NamedPipeServerStream(
                        pipeName: outPipeName,
                        direction: PipeDirection.Out,
                        maxNumberOfServerInstances: 1,
                        transmissionMode: PipeTransmissionMode.Byte,
                        options: (PipeOptions)CurrentUserOnly);

                return new NamedPipeServerStream(
                    pipeName: inOutPipeName,
                    direction: PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous | (PipeOptions)CurrentUserOnly);
            }

            // Now deal with Windows PowerShell
            // We need to use reflection to get a nice constructor

            var pipeSecurity = new PipeSecurity();

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                // Allow the Administrators group full access to the pipe.
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)),
                    PipeAccessRights.FullControl, AccessControlType.Allow));
            }
            else
            {
                // Allow the current user read/write access to the pipe.
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    WindowsIdentity.GetCurrent().User,
                    PipeAccessRights.ReadWrite, AccessControlType.Allow));
            }

            outPipe = outPipeName == null
                ? null
                : (NamedPipeServerStream)s_netFrameworkPipeServerConstructor.Invoke(
                    new object[] {
                        outPipeName,
                        PipeDirection.InOut,
                        1, // maxNumberOfServerInstances
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        1024, // inBufferSize
                        1024, // outBufferSize
                        pipeSecurity
                    });

            return (NamedPipeServerStream)s_netFrameworkPipeServerConstructor.Invoke(
                new object[] {
                    inOutPipeName,
                    PipeDirection.InOut,
                    1, // maxNumberOfServerInstances
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    1024, // inBufferSize
                    1024, // outBufferSize
                    pipeSecurity
                });
        }

        #endregion
    }
}

}
