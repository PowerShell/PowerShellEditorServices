using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    internal interface IPowerShellDebugContext
    {
        bool IsStopped { get; }

        DscBreakpointCapability DscBreakpointCapability { get; }

        DebuggerStopEventArgs LastStopEventArgs { get; }

        CancellationToken OnResumeCancellationToken { get; }

        void Continue();

        void StepOver();

        void StepInto();

        void StepOut();

        void Break();

        void Abort();
    }
}
