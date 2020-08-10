
using System.Management.Automation.Runspaces;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal interface IRunspaceContext
    {
        bool IsRemote { get; }

        RunspaceOrigin RunspaceOrigin { get; }
    }
}
