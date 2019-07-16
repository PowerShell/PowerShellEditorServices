//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Microsoft.PowerShell.EditorServices.Engine
{
    public enum EditorServicesHostStatus
    {
        Started,
        Failed,
        Ended
    }

    public enum EditorServiceTransportType
    {
        NamedPipe,
        Stdio
    }

    public class EditorServiceTransportConfig
    {
        public EditorServiceTransportType TransportType { get; set; }
        /// <summary>
        /// Configures the endpoint of the transport.
        /// For Stdio it's ignored.
        /// For NamedPipe it's the pipe name.
        /// </summary>
        public string InOutPipeName { get; set; }

        public string OutPipeName { get; set; }

        public string InPipeName { get; set; }

        internal string Endpoint => OutPipeName != null && InPipeName != null ? $"In pipe: {InPipeName} Out pipe: {OutPipeName}" : $" InOut pipe: {InOutPipeName}";
    }

    /// <summary>
    /// Provides a simplified interface for hosting the language and debug services
    /// over the named pipe server protocol.
    /// </summary>
    public class EditorServicesHost
    {
        #region Private Fields

        private readonly IServiceCollection _serviceCollection;

        private readonly HostDetails _hostDetails;

        private ILanguageServer _languageServer;

        private readonly Extensions.Logging.ILogger _logger;

        private readonly ILoggerFactory _factory;

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
            bool waitForDebugger,
            string[] additionalModules,
            string[] featureFlags)
            : this(
                hostDetails,
                bundledModulesPath,
                enableConsoleRepl,
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
            bool waitForDebugger,
            string[] additionalModules,
            string[] featureFlags,
            PSHost internalHost)
        {
            Validate.IsNotNull(nameof(hostDetails), hostDetails);
            Validate.IsNotNull(nameof(internalHost), internalHost);

            _serviceCollection = new ServiceCollection();

            Log.Logger = new LoggerConfiguration().Enrich.FromLogContext()
                            .WriteTo.Console()
                            .CreateLogger();
            _factory = new LoggerFactory().AddSerilog(Log.Logger);
            _logger = _factory.CreateLogger<EditorServicesHost>();

            _hostDetails = hostDetails;

            /*
            this.hostDetails = hostDetails;
            this.enableConsoleRepl = enableConsoleRepl;
            this.bundledModulesPath = bundledModulesPath;
            this.additionalModules = additionalModules ?? Array.Empty<string>();
            this.featureFlags = new HashSet<string>(featureFlags ?? Array.Empty<string>();
            this.serverCompletedTask = new TaskCompletionSource<bool>();
            this.internalHost = internalHost;
            */

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
            FileVersionInfo fileVersionInfo =
                FileVersionInfo.GetVersionInfo(this.GetType().GetTypeInfo().Assembly.Location);

            string osVersion = RuntimeInformation.OSDescription;

            string osArch = GetOSArchitecture();

            string buildTime = BuildInfo.BuildTime?.ToString("s", System.Globalization.CultureInfo.InvariantCulture) ?? "<unspecified>";

            string logHeader = $@"
PowerShell Editor Services Host v{fileVersionInfo.FileVersion} starting (PID {Process.GetCurrentProcess().Id}

  Host application details:

    Name:      {_hostDetails.Name}
    Version:   {_hostDetails.Version}
    ProfileId: {_hostDetails.ProfileId}
    Arch:      {osArch}

  Operating system details:

    Version: {osVersion}
    Arch:    {osArch}

  Build information:

    Version: {BuildInfo.BuildVersion}
    Origin:  {BuildInfo.BuildOrigin}
    Date:    {buildTime}
";

            _logger.LogInformation(logHeader);
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
            while (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}");
                Thread.Sleep(2000);
            }

            _logger.LogInformation($"LSP NamedPipe: {config.InOutPipeName}\nLSP OutPipe: {config.OutPipeName}");

            _serviceCollection.AddSingleton<WorkspaceService>();
            _serviceCollection.AddSingleton<SymbolsService>();
            _serviceCollection.AddSingleton<AnalysisService>(
                (provider) => {
                    // TODO: Fill in settings
                    return AnalysisService.Create(null, _factory.CreateLogger<AnalysisService>());
                }
            );

            _languageServer = new OmnisharpLanguageServerBuilder(_serviceCollection)
            {
                NamedPipeName = config.InOutPipeName ?? config.InPipeName,
                OutNamedPipeName = config.OutPipeName,
                LoggerFactory = _factory
            }
            .BuildLanguageServer();

            _logger.LogInformation("Starting language server");

            Task.Factory.StartNew(() => _languageServer.StartAsync(),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            _logger.LogInformation(
                string.Format(
                    "Language service started, type = {0}, endpoint = {1}",
                    config.TransportType, config.Endpoint));
        }

        /// <summary>
        /// Starts the debug service with the specified config.
        /// </summary>
        /// <param name="config">The config that contains information on the communication protocol that will be used.</param>
        /// <param name="profilePaths">The profiles that will be loaded in the session.</param>
        /// <param name="useExistingSession">Determines if we will reuse the session that we have.</param>
        public void StartDebugService(
            EditorServiceTransportConfig config,
            ProfilePaths profilePaths,
            bool useExistingSession)
        {
            /*
            this.debugServiceListener = CreateServiceListener(MessageProtocolType.DebugAdapter, config);
            this.debugServiceListener.ClientConnect += OnDebugServiceClientConnect;
            this.debugServiceListener.Start();

            this.logger.Write(
                LogLevel.Normal,
                string.Format(
                    "Debug service started, type = {0}, endpoint = {1}",
                    config.TransportType, config.Endpoint));
            */
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
            // TODO: We need a way to know when to complete this task!
            _languageServer.WaitForShutdown().Wait();
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

        #endregion
    }
}
