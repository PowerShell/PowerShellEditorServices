using PowerShellEditorServices.Hosting;
using System;
using System.Collections.Generic;
using System.Management.Automation.Host;
using System.Text;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public enum ConsoleReplKind
    {
        None = 0,
        LegacyReadLine,
        PSReadLine,
    }

    public class EditorServicesConfig
    {
        public EditorServicesConfig(
            HostInfo hostInfo,
            PSHost psHost,
            string sessionDetailsPath,
            string bundledModulePath,
            string logPath)
        {
            HostInfo = hostInfo;
            PSHost = psHost;
            SessionDetailsPath = sessionDetailsPath;
            BundledModulePath = bundledModulePath;
            LogPath = logPath;
        }

        public HostInfo HostInfo { get; }

        public PSHost PSHost { get; }

        public string SessionDetailsPath { get; }

        public string BundledModulePath { get; }

        public string LogPath { get; }

        public IReadOnlyList<string> AdditionalModules { get; set; } = null;

        public IReadOnlyList<string> FeatureFlags { get; set; } = null;

        public ConsoleReplKind ConsoleRepl { get; set; } = ConsoleReplKind.None;

        public PsesLogLevel LogLevel { get; set; } = PsesLogLevel.Normal;

        public ITransportConfig LanguageServiceTransport { get; set; } = null;

        public ITransportConfig DebugServiceTransport { get; set; } = null;

        public ProfilePathConfig ProfilePaths { get; set; } = null;
    }
}
