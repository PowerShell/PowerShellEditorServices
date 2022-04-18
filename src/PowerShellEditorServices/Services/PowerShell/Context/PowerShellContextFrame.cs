// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using SMA = System.Management.Automation;

#if DEBUG
using System.Text;
#endif

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    [DebuggerDisplay("{ToDebuggerDisplayString()}")]
    internal class PowerShellContextFrame : IDisposable
    {
        public static PowerShellContextFrame CreateForPowerShellInstance(
            ILogger logger,
            SMA.PowerShell pwsh,
            PowerShellFrameType frameType,
            string localComputerName)
        {
            RunspaceInfo runspaceInfo = RunspaceInfo.CreateFromPowerShell(logger, pwsh, localComputerName);
            return new PowerShellContextFrame(pwsh, runspaceInfo, frameType);
        }

        private bool disposedValue;

        public PowerShellContextFrame(SMA.PowerShell powerShell, RunspaceInfo runspaceInfo, PowerShellFrameType frameType)
        {
            PowerShell = powerShell;
            RunspaceInfo = runspaceInfo;
            FrameType = frameType;
        }

        public SMA.PowerShell PowerShell { get; }

        public RunspaceInfo RunspaceInfo { get; }

        public PowerShellFrameType FrameType { get; }

        public bool IsRepl => (FrameType & PowerShellFrameType.Repl) is not 0;

        public bool IsRemote => (FrameType & PowerShellFrameType.Remote) is not 0;

        public bool IsNested => (FrameType & PowerShellFrameType.Nested) is not 0;

        public bool IsDebug => (FrameType & PowerShellFrameType.Debug) is not 0;

        public bool IsAwaitingPop { get; set; }

        public bool SessionExiting { get; set; }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // When runspace is popping from `Exit-PSHostProcess` or similar, attempting
                    // to dispose directly in the same frame would dead lock.
                    if (SessionExiting)
                    {
                        PowerShell.DisposeWhenCompleted();
                    }
                    else
                    {
                        PowerShell.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

#if DEBUG
        private string ToDebuggerDisplayString()
        {
            StringBuilder text = new();

            if ((FrameType & PowerShellFrameType.Nested) is not 0)
            {
                text.Append("Ne-");
            }

            if ((FrameType & PowerShellFrameType.Debug) is not 0)
            {
                text.Append("De-");
            }

            if ((FrameType & PowerShellFrameType.Remote) is not 0)
            {
                text.Append("Rem-");
            }

            if ((FrameType & PowerShellFrameType.NonInteractive) is not 0)
            {
                text.Append("NI-");
            }

            if ((FrameType & PowerShellFrameType.Repl) is not 0)
            {
                text.Append("Repl-");
            }

            text.Append(PowerShellDebugDisplay.ToDebuggerString(PowerShell));
            return text.ToString();
        }
#endif
    }
}
