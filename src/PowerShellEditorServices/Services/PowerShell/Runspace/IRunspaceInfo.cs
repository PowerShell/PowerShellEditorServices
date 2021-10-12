
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using SMA = System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace
{
    internal interface IRunspaceInfo
    {
        RunspaceOrigin RunspaceOrigin { get; }

        bool IsOnRemoteMachine { get; }

        PowerShellVersionDetails PowerShellVersionDetails { get; }

        SessionDetails SessionDetails { get; }

        SMA.Runspace Runspace { get; }
    }
}
