using Microsoft.PowerShell.Commands;
using PowerShellEditorServices.Hosting;
using System;
using System.Linq;
using System.Management.Automation;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
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

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string SessionDetailsPath { get; set; }

        [Parameter(ParameterSetName = "NamedPipe")]
        public string LanguageServicePipeName { get; set; }

        [Parameter(ParameterSetName = "NamedPipe")]
        public string DebugServicePipeName { get; set; }

        [Parameter(ParameterSetName = "NamedPipeSimplex")]
        public string LanguageServiceInPipeName { get; set; }

        [Parameter(ParameterSetName = "NamedPipeSimplex")]
        public string LanguageServiceOutPipeName { get; set; }

        [Parameter(ParameterSetName = "NamedPipeSimplex")]
        public string DebugServiceInPipeName { get; set; }

        [Parameter(ParameterSetName = "NamedPipeSimplex")]
        public string DebugServiceOutPipeName { get; set; }

        [Parameter(ParameterSetName = "Stdio")]
        public SwitchParameter Stdio { get; set; }

        /// <summary>
        /// The path to where PowerShellEditorServices and its bundled modules are.
        /// </summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string BundledModulesPath { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string LogPath { get; set; }

        [Parameter]
        public PsesLogLevel LogLevel { get; set; }

        [Parameter]
        public string[] AdditionalModules { get; set; }

        [Parameter]
        public string[] FeatureFlags { get; set; }

        [Parameter]
        public SwitchParameter EnableConsoleRepl { get; set; }

        [Parameter]
        public SwitchParameter UseLegacyReadLine { get; set; }

        [Parameter]
        public SwitchParameter DebugServiceOnly { get; set; }

        [Parameter]
        public SwitchParameter WaitForDebugger { get; set; }

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
                    System.Threading.Thread.Sleep(500);
                }
            }
#endif

            _logger = new HostLogger(LogLevel);
            _logger.Subscribe(new PSHostLogger(Host.UI));

            _logger.Log(PsesLogLevel.Diagnostic, "Logger created");
        }

        protected override void EndProcessing()
        {
            _logger.Log(PsesLogLevel.Diagnostic, "Beginning EndProcessing block");

            var sessionFileWriter = new SessionFileWriter(_logger, SessionDetailsPath);

            _logger.Log(PsesLogLevel.Diagnostic, "Session file writer created");

            try
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

                _logger.Log(PsesLogLevel.Verbose, "Loading EditorServices");
                using (var psesLoader = EditorServicesLoader.Create(_logger, editorServicesConfig, sessionFileWriter))
                {
                    psesLoader.LoadAndRunEditorServicesAsync().Wait();
                }
            }
            catch (Exception e)
            {
                _logger.LogException("Exception encountered starting EditorServices", e);

                // Give the user a chance to read the message
                Console.ReadKey();

                throw;
            }
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
