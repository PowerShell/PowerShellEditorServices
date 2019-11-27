using PowerShellEditorServices.Host;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Host
{
    public enum ConsoleReplKind
    {
        None = 0,
        PSReadLine,
        LegacyReadLine,
    }

    public abstract class EditorServicesConfiguration
    {
        public string HostName { get; set; }

        public string HostProfileId { get; set; }

        public string HostVersion { get; set; }

        public ConsoleReplKind ConsoleRepl { get; set; }

        public string BundledModulesPath { get; set; }

        public IReadOnlyList<string> AdditionalModules { get; set; }

        public IReadOnlyList<string> FeatureFlags { get; set; }

        public string LogPath { get; set; }

        public PsesLogLevel LogLevel { get; set; }

        public string SessionDetailsPath { get; set; }
    }

    public class EditorServicesHost
    {
    }
}
