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
        public string BundledModulePath { get; set; }

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

        protected override void EndProcessing()
        {
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
                }
            }

            var hostDetails = new HostDetails(HostName, HostProfileId, HostVersion);
            var editorServicesConfig = new EditorServicesConfig(hostDetails, SessionDetailsPath, BundledModulePath, LogPath)
            {
                FeatureFlags = FeatureFlags,
                LogLevel = LogLevel,
                ConsoleRepl = GetReplKind(),
                WaitForDebugger = WaitForDebugger,
                AdditionalModules = AdditionalModules,
                LanguageServiceTransport = GetLanguageServiceTransport(),
                DebugServiceTransport = GetDebugServiceTransport(),
            };

            using (var psesLoader = EditorServicesLoader.Create(editorServicesConfig))
            {
                psesLoader.LoadAndRunEditorServicesAsync().Wait();
            }
        }

        private ConsoleReplKind GetReplKind()
        {
            if (Stdio || !EnableConsoleRepl)
            {
                return ConsoleReplKind.None;
            }

            if (UseLegacyReadLine)
            {
                return ConsoleReplKind.LegacyReadLine;
            }

            return ConsoleReplKind.PSReadLine;
        }

        private ITransportConfig GetLanguageServiceTransport()
        {
            if (DebugServiceOnly)
            {
                return null;
            }

            if (Stdio)
            {
                return new StdioTransportConfig();
            }

            if (LanguageServicePipeName != null)
            {
                return new DuplexNamedPipeTransportConfig(LanguageServicePipeName);
            }

            return new SimplexNamedPipeTransportConfig(LanguageServiceInPipeName, LanguageServiceOutPipeName);
        }

        private ITransportConfig GetDebugServiceTransport()
        {
            if (Stdio)
            {
                return DebugServiceOnly
                    ? new StdioTransportConfig()
                    : null;
            }

            if (DebugServicePipeName != null)
            {
                return new DuplexNamedPipeTransportConfig(DebugServicePipeName);
            }

            return new SimplexNamedPipeTransportConfig(DebugServiceInPipeName, DebugServiceOutPipeName);
        }
    }
}
