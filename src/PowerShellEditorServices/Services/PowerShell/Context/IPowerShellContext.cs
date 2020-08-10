using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    internal interface IPowerShellContext : IDisposable
    {
        SMA.PowerShell CurrentPowerShell { get; }

        bool IsRunspacePushed { get; }

        void SetShouldExit(int exitCode);

        void ProcessDebuggerResult(DebuggerCommandResults debuggerResult);

        void PushNestedPowerShell();

        void PushPowerShell(Runspace runspaceToUse);
    }
}
