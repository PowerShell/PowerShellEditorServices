using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using SMA = System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    internal class RunspaceInfo : IRunspaceInfo
    {
        public RunspaceInfo(
            SMA.Runspace runspace,
            RunspaceOrigin origin,
            PowerShellVersionDetails powerShellVersionDetails,
            SessionDetails sessionDetails,
            DscBreakpointCapability dscBreakpointCapability)
        {
            Runspace = runspace;
            RunspaceOrigin = origin;
            SessionDetails = sessionDetails;
            PowerShellVersionDetails = powerShellVersionDetails;
            DscBreakpointCapability = dscBreakpointCapability;
        }

        public RunspaceOrigin RunspaceOrigin { get; }

        public PowerShellVersionDetails PowerShellVersionDetails { get; }

        public SessionDetails SessionDetails { get; }

        public SMA.Runspace Runspace { get; }

        public DscBreakpointCapability DscBreakpointCapability { get; }
    }
}
