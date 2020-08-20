using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using System;
using System.Management.Automation;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    using System.Management.Automation.Runspaces;

    internal interface IPowerShellContext : IDisposable
    {
        CancellationTokenSource CurrentCancellationSource { get; }

        EditorServicesConsolePSHost EditorServicesPSHost { get; }

        bool IsRunspacePushed { get; }

        string InitialWorkingDirectory { get; }
    }
}
