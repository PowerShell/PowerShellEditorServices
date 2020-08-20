
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using SMA = System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    internal interface IRunspaceInfo
    {
        RunspaceOrigin RunspaceOrigin { get; }

        PowerShellVersionDetails PowerShellVersionDetails { get; }

        SessionDetails SessionDetails { get; }

        SMA.Runspace Runspace { get; }

        DscBreakpointCapability DscBreakpointCapability { get; }
    }

    internal static class RunspaceInfoExtensions
    {
        public static bool IsRemote(this IRunspaceInfo runspaceInfo)
        {
            return runspaceInfo.RunspaceOrigin != RunspaceOrigin.Local;
        }
    }
}
