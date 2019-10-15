//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Server;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Utility;
using Serilog;

namespace Microsoft.PowerShell.EditorServices.Hosting
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

        // This int will be casted to a PipeOptions enum that only exists in .NET Core 2.1 and up which is why it's not available to us in .NET Standard.
        private const int CurrentUserOnly = 0x20000000;

        // In .NET Framework, NamedPipeServerStream has a constructor that takes in a PipeSecurity object. We will use reflection to call the constructor,
        // since .NET Framework doesn't have the `CurrentUserOnly` PipeOption.
        // doc: https://docs.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.-ctor?view=netframework-4.7.2#System_IO_Pipes_NamedPipeServerStream__ctor_System_String_System_IO_Pipes_PipeDirection_System_Int32_System_IO_Pipes_PipeTransmissionMode_System_IO_Pipes_PipeOptions_System_Int32_System_Int32_System_IO_Pipes_PipeSecurity_
        private static readonly ConstructorInfo s_netFrameworkPipeServerConstructor =
            typeof(NamedPipeServerStream).GetConstructor(new[] { typeof(string), typeof(PipeDirection), typeof(int), typeof(PipeTransmissionMode), typeof(PipeOptions), typeof(int), typeof(int), typeof(PipeSecurity) });

        private readonly HostDetails _hostDetails;

        private readonly PSHost _internalHost;

        private readonly bool _enableConsoleRepl;

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

            _hostDetails = hostDetails;

            //this._hostDetails = hostDetails;
            _enableConsoleRepl = enableConsoleRepl;
            //this.bundledModulesPath = bundledModulesPath;
            _additionalModules = additionalModules ?? Array.Empty<string>();
            _featureFlags = new HashSet<string>(featureFlags ?? Array.Empty<string>());
            //this.serverCompletedTask = new TaskCompletionSource<bool>();
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
            Log.Logger = new LoggerConfiguration().Enrich.FromLogContext()
                            .WriteTo.File(logFilePath)
                            .MinimumLevel.Verbose()
                            .CreateLogger();
            _factory = new LoggerFactory().AddSerilog(Log.Logger);
            _logger = _factory.CreateLogger<EditorServicesHost>();

            FileVersionInfo fileVersionInfo =
                FileVersionInfo.GetVersionInfo(this.GetType().GetTypeInfo().Assembly.Location);

            string osVersion = RuntimeInformation.OSDescription;

            string osArch = GetOSArchitecture();

            string buildTime = BuildInfo.BuildTime?.ToString("s", System.Globalization.CultureInfo.InvariantCulture) ?? "<unspecified>";

            string logHeader = $@"
PowerShell Editor Services Host v{fileVersionInfo.FileVersion} starting (PID {Process.GetCurrentProcess().Id})

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
            _logger.LogInformation($"LSP NamedPipe: {config.InOutPipeName}\nLSP OutPipe: {config.OutPipeName}");

            switch (config.TransportType)
            {
                case EditorServiceTransportType.NamedPipe:
                    _languageServer = new NamedPipePsesLanguageServer(
                        _factory,
                        LogLevel.Trace,
                        _enableConsoleRepl,
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
        /// <param name="useExistingSession">Determines if we will reuse the session that we have.</param>
        public void StartDebugService(
            EditorServiceTransportConfig config,
            ProfilePaths profilePaths,
            bool useExistingSession)
        {
            _logger.LogInformation($"Debug NamedPipe: {config.InOutPipeName}\nDebug OutPipe: {config.OutPipeName}");

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
                            await _debugServer.StartAsync(_languageServer.LanguageServer.Services);
                            _logger.LogInformation(
                                $"Debug service started, type = {config.TransportType}, endpoint = {config.Endpoint}");
                        });

                    break;

                case EditorServiceTransportType.Stdio:
                    _debugServer = new PsesDebugServer(
                        _factory,
                        Console.OpenStandardInput(),
                        Console.OpenStandardOutput());

                    Task.Run(async () =>
                    {
                        _logger.LogInformation("Starting debug server");

                        IServiceProvider serviceProvider = useExistingSession
                            ? _languageServer.LanguageServer.Services
                            : new ServiceCollection().AddSingleton<PowerShellContextService>(
                                (provider) => PowerShellContextService.Create(
                                    _factory,
                                    provider.GetService<OmniSharp.Extensions.LanguageServer.Protocol.Server.ILanguageServer>(),
                                    profilePaths,
                                    _featureFlags,
                                    _enableConsoleRepl,
                                    _internalHost,
                                    _hostDetails,
                                    _additionalModules))
                                .BuildServiceProvider();

                        await _debugServer.StartAsync(serviceProvider);
                        _logger.LogInformation(
                            $"Debug service started, type = {config.TransportType}, endpoint = {config.Endpoint}");
                    });
                    break;

                default:
                    throw new NotSupportedException($"The transport {config.TransportType} is not supported");
            }

            if(!alreadySubscribedDebug)
            {
                alreadySubscribedDebug = true;
                _debugServer.SessionEnded += (sender, eventArgs) =>
                {
                    _debugServer.Dispose();
                    alreadySubscribedDebug = false;
                    StartDebugService(config, profilePaths, useExistingSession);
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
