//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.Commands;
using System;
using System.Linq;
using System.Management.Automation;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    /// <summary>
    /// The Start-EditorServices command, the conventional entrypoint for PowerShell Editor Services.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Start, "EditorServices", DefaultParameterSetName = "NamedPipe")]
    public sealed class StartEditorServicesCommand : PSCmdlet
    {
        private HostLogger _logger;

        /// <summary>
        /// The name of the EditorServices host to report
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
        public PsesLogLevel LogLevel { get; set; }

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

        protected override void BeginProcessing()
        {
#if DEBUG
            if (WaitForDebugger)
            {
                while (!System.Diagnostics.Debugger.IsAttached)
                {
                    Console.WriteLine($"PID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
                    System.Threading.Thread.Sleep(1000);
                }
            }
#endif

            // Set up logging now for use throughout startup
            _logger = new HostLogger(LogLevel);
            _logger.Subscribe(new PSHostLogger(Host.UI));
            _logger.Log(PsesLogLevel.Diagnostic, "Logger created");
        }

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

                using (var psesLoader = EditorServicesLoader.Create(_logger, editorServicesConfig, sessionFileWriter))
                {
                    _logger.Log(PsesLogLevel.Verbose, "Loading EditorServices");
                    psesLoader.LoadAndRunEditorServicesAsync().Wait();
                }
            }
            catch (Exception e)
            {
                _logger.LogException("Exception encountered starting EditorServices", e);

                // Give the user a chance to read the message
                Console.ReadKey();

                ThrowTerminatingError(new ErrorRecord(e, "PowerShellEditorServicesError", ErrorCategory.NotSpecified, this));
            }
        }

        private void RemovePSReadLineForStartup()
        {
            _logger.Log(PsesLogLevel.Verbose, "Removing PSReadLine");
            using (var pwsh = SMA.PowerShell.Create())
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
            var hostInfo = new HostInfo(HostName, HostProfileId, HostVersion);
            var editorServicesConfig = new EditorServicesConfig(hostInfo, Host, SessionDetailsPath, BundledModulesPath, LogPath)
            {
                FeatureFlags = FeatureFlags,
                LogLevel = LogLevel,
                ConsoleRepl = GetReplKind(),
                AdditionalModules = AdditionalModules,
                LanguageServiceTransport = GetLanguageServiceTransport(),
                DebugServiceTransport = GetDebugServiceTransport(),
            };

            return editorServicesConfig;
        }

        private ConsoleReplKind GetReplKind()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Determining REPL kind");

            if (Stdio || !EnableConsoleRepl)
            {
                _logger.Log(PsesLogLevel.Diagnostic, "REPL configured as None");
                return ConsoleReplKind.None;
            }

            if (UseLegacyReadLine)
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
                return new StdioTransportConfig();
            }

            if (LanguageServiceInPipeName != null && LanguageServiceOutPipeName != null)
            {
                return SimplexNamedPipeTransportConfig.Create(LanguageServiceInPipeName, LanguageServiceOutPipeName);
            }

            if (SplitInOutPipes)
            {
                return SimplexNamedPipeTransportConfig.Create(LanguageServicePipeName);
            }

            return DuplexNamedPipeTransportConfig.Create(LanguageServicePipeName);
        }

        private ITransportConfig GetDebugServiceTransport()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Configuring debug transport");

            if (Stdio)
            {
                if (DebugServiceOnly)
                {
                    return new StdioTransportConfig();
                }

                _logger.Log(PsesLogLevel.Diagnostic, "No debug transport: Transport is Stdio with debug disabled");
                return null;
            }

            if (DebugServiceInPipeName != null && DebugServiceOutPipeName != null)
            {
                return SimplexNamedPipeTransportConfig.Create(DebugServiceInPipeName, DebugServiceOutPipeName);
            }

            if (SplitInOutPipes)
            {
                return SimplexNamedPipeTransportConfig.Create(DebugServicePipeName);
            }

            return DuplexNamedPipeTransportConfig.Create(DebugServicePipeName);
        }
    }
}
