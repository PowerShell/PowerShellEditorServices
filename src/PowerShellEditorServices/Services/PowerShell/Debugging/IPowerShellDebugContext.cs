using System;
using System.Management.Automation;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    internal interface IPowerShellDebugContext
    {
        bool IsStopped { get; }

        DscBreakpointCapability DscBreakpointCapability { get; }

        DebuggerStopEventArgs LastStopEventArgs { get; }

        CancellationToken OnResumeCancellationToken { get; }

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;

        public event Action<object, DebuggerResumingEventArgs> DebuggerResuming;

        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        void Continue();

        void StepOver();

        void StepInto();

        void StepOut();

        void BreakExecution();

        void Abort();
    }
}
