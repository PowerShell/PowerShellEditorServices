using PowerShellEditorServices.Hosting;
using System;
using System.Collections.Generic;
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
        private ProfilePaths _profilePaths;

        public EditorServicesConfig(
            HostDetails hostDetails,
            string sessionDetailsPath,
            string bundledModulePath,
            string logPath)
        {
            HostDetails = hostDetails;
            SessionDetailsPath = sessionDetailsPath;
            BundledModulePath = bundledModulePath;
            LogPath = logPath;
        }

        public HostDetails HostDetails { get; }

        public string SessionDetailsPath { get; }

        public string BundledModulePath { get; }

        public string LogPath { get; }

        public ProfilePaths ProfilePaths { get; set; } = null;

        public IReadOnlyList<string> AdditionalModules { get; set; } = null;

        public IReadOnlyList<string> FeatureFlags { get; set; } = null;

        public ConsoleReplKind ConsoleRepl { get; set; } = ConsoleReplKind.None;

        public PsesLogLevel LogLevel { get; set; } = PsesLogLevel.Normal;

        public bool WaitForDebugger { get; set; } = false;

        public ITransportConfig LanguageServiceTransport { get; set; } = null;

        public ITransportConfig DebugServiceTransport { get; set; } = null;
    }
}
