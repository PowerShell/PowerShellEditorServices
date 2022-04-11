// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging
{
    internal interface IPowerShellDebugContext
    {
        bool IsStopped { get; }

        DebuggerStopEventArgs LastStopEventArgs { get; }

        public bool IsDebuggingRemoteRunspace { get; set; }

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;

        public event Action<object, DebuggerResumingEventArgs> DebuggerResuming;

        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        void Continue();

        void StepOver();

        void StepInto();

        void StepOut();

        void BreakExecution();

        void Abort();

        Task<DscBreakpointCapability> GetDscBreakpointCapabilityAsync(CancellationToken cancellationToken);
    }
}
