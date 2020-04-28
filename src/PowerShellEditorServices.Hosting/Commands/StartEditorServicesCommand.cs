//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using SMA = System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.EditorServices.Hosting;
using System.Globalization;
using System.Collections;

// TODO: Remove this when we drop support for PS6.
#if CoreCLR
using System.Runtime.InteropServices;
#endif

#if DEBUG
using System.Diagnostics;
using System.Threading;

using Debugger = System.Diagnostics.Debugger;
#endif

namespace Microsoft.PowerShell.EditorServices.Commands
{
    /// <summary>
    /// The Start-EditorServices command, the conventional entrypoint for PowerShell Editor Services.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Cmdlet parameters can be arrays")]
    [Cmdlet(VerbsLifecycle.Start, "EditorServices", DefaultParameterSetName = "NamedPipe")]
    public sealed class StartEditorServicesCommand : PSCmdlet
    {
        // TODO: Remove this when we drop support for PS6.
        private static bool s_isWindows =
#if CoreCLR
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
        true;
#endif

        private readonly List<IDisposable> _disposableResources;

        private readonly List<IDisposable> _loggerUnsubscribers;

        private HostLogger _logger;

        public StartEditorServicesCommand()
        {
            //Sets the distribution channel to "PSES" so starts can be distinguished in PS7+ telemetry
            Environment.SetEnvironmentVariable("POWERSHELL_DISTRIBUTION_CHANNEL", "PSES");
            _disposableResources = new List<IDisposable>();
            _loggerUnsubscribers = new List<IDisposable>();
        }

        /// <summary>
        /// The name of the EditorServices host to report.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string HostName { get; set; }

        /// <summary>
        /// The ID to give to the host's profile.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string HostProfileId { get; set; }

        /// <summary>
        /// The version to report for the host.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public Version HostVersion { get; set; }

        /// <summary>
        /// Path to the session file to create on startup or startup failure.
        /// </summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string SessionDetailsPath { get; set; }

        /// <summary>
        /// The name of the named pipe to use for the LSP transport.
        /// If left unset and named pipes are used as transport, a new name will be generated.
        /// </summary>
        [Parameter(ParameterSetName = "NamedPipe")]
        public string LanguageServicePipeName { get; set; }

        /// <summary>
        /// The name of the named pipe to use for the debug adapter transport.
        /// If left unset and named pipes are used as a transport, a new name will be generated.
        /// </summary>
        [Parameter(ParameterSetName = "NamedPipe")]
        public string DebugServicePipeName { get; set; }

        /// <summary>
        /// The name of the input named pipe to use for the LSP transport.
        /// </summary>
        [Parameter(ParameterSetName = "NamedPipeSimplex")]
        public string LanguageServiceInPipeName { get; set; }

        /// <summary>
        /// The name of the output named pipe to use for the LSP transport.
        /// </summary>
        [Parameter(ParameterSetName = "NamedPipeSimplex")]
        public string LanguageServiceOutPipeName { get; set; }

        /// <summary>
        /// The name of the input pipe to use for the debug adapter transport.
        /// </summary>
        [Parameter(ParameterSetName = "NamedPipeSimplex")]
        public string DebugServiceInPipeName { get; set; }

        /// <summary>
        /// The name of the output pipe to use for the debug adapter transport.
        /// </summary>
        [Parameter(ParameterSetName = "NamedPipeSimplex")]
        public string DebugServiceOutPipeName { get; set; }

        /// <summary>
        /// If set, uses standard input/output as the LSP transport.
        /// When <see cref="DebugServiceOnly"/> is set with this, standard input/output
        /// is used as the debug adapter transport.
        /// </summary>
        [Parameter(ParameterSetName = "Stdio")]
        public SwitchParameter Stdio { get; set; }

        /// <summary>
        /// The path to where PowerShellEditorServices and its bundled modules are.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string BundledModulesPath { get; set; }

        /// <summary>
        /// The absolute path to the where the editor services log file should be created and logged to.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string LogPath { get; set; }

        /// <summary>
        /// The minimum log level that should be emitted.
        /// </summary>
        [Parameter]
        public PsesLogLevel LogLevel { get; set; } = PsesLogLevel.Normal;

        /// <summary>
        /// Paths to additional PowerShell modules to be imported at startup.
        /// </summary>
        [Parameter]
        public string[] AdditionalModules { get; set; }

        /// <summary>
        /// Any feature flags to enable in EditorServices.
        /// </summary>
        [Parameter]
        public string[] FeatureFlags { get; set; }

        /// <summary>
        /// When set, enables the integrated console.
        /// </summary>
        [Parameter]
        public SwitchParameter EnableConsoleRepl { get; set; }

        /// <summary>
        /// When set and the console is enabled, the legacy lightweight
        /// readline implementation will be used instead of PSReadLine.
        /// </summary>
        [Parameter]
        public SwitchParameter UseLegacyReadLine { get; set; }

        /// <summary>
        /// When set, do not enable LSP service, only the debug adapter.
        /// </summary>
        [Parameter]
        public SwitchParameter DebugServiceOnly { get; set; }

        /// <summary>
        /// When set with a debug build, startup will wait for a debugger to attach.
        /// </summary>
        [Parameter]
        public SwitchParameter WaitForDebugger { get; set; }

        /// <summary>
        /// When set, will generate two simplex named pipes using a single named pipe name.
        /// </summary>
        [Parameter]
        public SwitchParameter SplitInOutPipes { get; set; }

        /// <summary>
        /// The banner/logo to display when the Integrated Console is first started.
        /// </summary>
        [Parameter]
        public string StartupBanner { get; set; }

        protected override void BeginProcessing()
        {
#if DEBUG
            if (WaitForDebugger)
            {
                while (!Debugger.IsAttached)
                {
                    Console.WriteLine($"PID: {Process.GetCurrentProcess().Id}");
                    Thread.Sleep(1000);
                }
            }
#endif

            // Set up logging now for use throughout startup
            StartLogging();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Uses ThrowTerminatingError() instead")]
        protected override void EndProcessing()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Beginning EndProcessing block");

            try
            {
                // First try to remove PSReadLine to decomplicate startup
                // If PSReadLine is enabled, it will be re-imported later
                RemovePSReadLineForStartup();

                // Create the configuration from parameters
                EditorServicesConfig editorServicesConfig = CreateConfigObject();

                var sessionFileWriter = new SessionFileWriter(_logger, SessionDetailsPath);
                _logger.Log(PsesLogLevel.Diagnostic, "Session file writer created");

                using (var psesLoader = EditorServicesLoader.Create(_logger, editorServicesConfig, sessionFileWriter, _loggerUnsubscribers))
                {
                    _logger.Log(PsesLogLevel.Verbose, "Loading EditorServices");
                    // Start editor services and wait here until it shuts down
                    psesLoader.LoadAndRunEditorServicesAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception e)
            {
                _logger.LogException("Exception encountered starting EditorServices", e);

                // Give the user a chance to read the message if they have a console
                if (!Stdio)
                {
                    Host.UI.WriteLine("\n== Press any key to close terminal ==");
                    Console.ReadKey();
                }

                ThrowTerminatingError(new ErrorRecord(e, "PowerShellEditorServicesError", ErrorCategory.NotSpecified, this));
            }
            finally
            {
                foreach (IDisposable disposableResource in _disposableResources)
                {
                    disposableResource.Dispose();
                }
            }
        }

        private void StartLogging()
        {
            _logger = new HostLogger(LogLevel);

            // We need to not write log messages to Stdio
            // if it's being used as a protocol transport
            if (!Stdio)
            {
                var hostLogger = new PSHostLogger(Host.UI);
                _loggerUnsubscribers.Add(_logger.Subscribe(hostLogger));
            }

            string logDirPath = GetLogDirPath();
            string logPath = Path.Combine(logDirPath, "StartEditorServices.log");

            // Temp debugging sessions may try to reuse this same path,
            // so we ensure they have a unique path
            if (File.Exists(logPath))
            {
                int randomInt = new Random().Next();
                logPath = Path.Combine(logDirPath, $"StartEditorServices-temp{randomInt.ToString("X", CultureInfo.InvariantCulture.NumberFormat)}.log");
            }

            var fileLogger = StreamLogger.CreateWithNewFile(logPath);
            _disposableResources.Add(fileLogger);
            IDisposable fileLoggerUnsubscriber = _logger.Subscribe(fileLogger);
            fileLogger.AddUnsubscriber(fileLoggerUnsubscriber);
            _loggerUnsubscribers.Add(fileLoggerUnsubscriber);

            _logger.Log(PsesLogLevel.Diagnostic, "Logging started");
        }

        private string GetLogDirPath()
        {
            string logDir = !string.IsNullOrEmpty(LogPath)
                ? Path.GetDirectoryName(LogPath)
                : Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            // Ensure logDir exists
            Directory.CreateDirectory(logDir);

            return logDir;
        }

        private void RemovePSReadLineForStartup()
        {
            _logger.Log(PsesLogLevel.Verbose, "Removing PSReadLine");
            using (var pwsh = SMA.PowerShell.Create(RunspaceMode.CurrentRunspace))
            {
                bool hasPSReadLine = pwsh.AddCommand(new CmdletInfo("Microsoft.PowerShell.Core\\Get-Module", typeof(GetModuleCommand)))
                    .AddParameter("Name", "PSReadLine")
                    .Invoke()
                    .Any();

                if (hasPSReadLine)
                {
                    pwsh.Commands.Clear();

                    pwsh.AddCommand(new CmdletInfo("Microsoft.PowerShell.Core\\Remove-Module", typeof(RemoveModuleCommand)))
                        .AddParameter("Name", "PSReadLine")
                        .AddParameter("ErrorAction", "SilentlyContinue");

                    _logger.Log(PsesLogLevel.Verbose, "Removed PSReadLine");
                }
            }
        }

        private EditorServicesConfig CreateConfigObject()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Creating host configuration");

            string bundledModulesPath = BundledModulesPath;
            if (!Path.IsPathRooted(bundledModulesPath))
            {
                // For compatibility, the bundled modules path is relative to the PSES bin directory
                // Ideally it should be one level up, the PSES module root
                bundledModulesPath = Path.GetFullPath(
                    Path.Combine(
                        Assembly.GetExecutingAssembly().Location,
                        "..",
                        bundledModulesPath));
            }

            var profile = (PSObject)GetVariableValue("profile");

            var hostInfo = new HostInfo(HostName, HostProfileId, HostVersion);
            var editorServicesConfig = new EditorServicesConfig(hostInfo, Host, SessionDetailsPath, bundledModulesPath, LogPath)
            {
                FeatureFlags = FeatureFlags,
                LogLevel = LogLevel,
                ConsoleRepl = GetReplKind(),
                AdditionalModules = AdditionalModules,
                LanguageServiceTransport = GetLanguageServiceTransport(),
                DebugServiceTransport = GetDebugServiceTransport(),
                LanguageMode = Runspace.DefaultRunspace.SessionStateProxy.LanguageMode,
                ProfilePaths = new ProfilePathConfig
                {
                    AllUsersAllHosts = GetProfilePathFromProfileObject(profile, ProfileUserKind.AllUsers, ProfileHostKind.AllHosts),
                    AllUsersCurrentHost = GetProfilePathFromProfileObject(profile, ProfileUserKind.AllUsers, ProfileHostKind.CurrentHost),
                    CurrentUserAllHosts = GetProfilePathFromProfileObject(profile, ProfileUserKind.CurrentUser, ProfileHostKind.AllHosts),
                    CurrentUserCurrentHost = GetProfilePathFromProfileObject(profile, ProfileUserKind.CurrentUser, ProfileHostKind.CurrentHost),
                },
            };

            if (StartupBanner != null)
            {
                editorServicesConfig.StartupBanner = StartupBanner;
            }

            return editorServicesConfig;
        }

        private string GetProfilePathFromProfileObject(PSObject profileObject, ProfileUserKind userKind, ProfileHostKind hostKind)
        {
            string profilePathName = $"{userKind}{hostKind}";

            string pwshProfilePath = (string)profileObject.Properties[profilePathName].Value;

            if (hostKind == ProfileHostKind.AllHosts)
            {
                return pwshProfilePath;
            }

            return Path.Combine(
                Path.GetDirectoryName(pwshProfilePath),
                $"{HostProfileId}_profile.ps1");
        }

        // We should only use PSReadLine if we specificied that we want a console repl
        // and we have not explicitly said to use the legacy ReadLine.
        // We also want it if we are either:
        // * On Windows on any version OR
        // * On Linux or macOS on any version greater than or equal to 7
        private ConsoleReplKind GetReplKind()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Determining REPL kind");

            if (Stdio || !EnableConsoleRepl)
            {
                _logger.Log(PsesLogLevel.Diagnostic, "REPL configured as None");
                return ConsoleReplKind.None;
            }

            // TODO: Remove this when we drop support for PS6.
            var psVersionTable = (Hashtable) this.SessionState.PSVariable.GetValue("PSVersionTable");
            dynamic version = psVersionTable["PSVersion"];
            var majorVersion = (int) version.Major;

            if (UseLegacyReadLine || (!s_isWindows && majorVersion == 6))
            {
                _logger.Log(PsesLogLevel.Diagnostic, "REPL configured as Legacy");
                return ConsoleReplKind.LegacyReadLine;
            }

            _logger.Log(PsesLogLevel.Diagnostic, "REPL configured as PSReadLine");
            return ConsoleReplKind.PSReadLine;
        }

        private ITransportConfig GetLanguageServiceTransport()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Configuring LSP transport");

            if (DebugServiceOnly)
            {
                _logger.Log(PsesLogLevel.Diagnostic, "No LSP transport: PSES is debug only");
                return null;
            }

            if (Stdio)
            {
                return new StdioTransportConfig(_logger);
            }

            if (LanguageServiceInPipeName != null && LanguageServiceOutPipeName != null)
            {
                return SimplexNamedPipeTransportConfig.Create(_logger, LanguageServiceInPipeName, LanguageServiceOutPipeName);
            }

            if (SplitInOutPipes)
            {
                return SimplexNamedPipeTransportConfig.Create(_logger, LanguageServicePipeName);
            }

            return DuplexNamedPipeTransportConfig.Create(_logger, LanguageServicePipeName);
        }

        private ITransportConfig GetDebugServiceTransport()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Configuring debug transport");

            if (Stdio)
            {
                if (DebugServiceOnly)
                {
                    return new StdioTransportConfig(_logger);
                }

                _logger.Log(PsesLogLevel.Diagnostic, "No debug transport: Transport is Stdio with debug disabled");
                return null;
            }

            if (DebugServiceInPipeName != null && DebugServiceOutPipeName != null)
            {
                return SimplexNamedPipeTransportConfig.Create(_logger, DebugServiceInPipeName, DebugServiceOutPipeName);
            }

            if (SplitInOutPipes)
            {
                return SimplexNamedPipeTransportConfig.Create(_logger, DebugServicePipeName);
            }

            return DuplexNamedPipeTransportConfig.Create(_logger, DebugServicePipeName);
        }

        private enum ProfileHostKind
        {
            AllHosts,
            CurrentHost,
        }

        private enum ProfileUserKind
        {
            AllUsers,
            CurrentUser,
        }
    }
}
