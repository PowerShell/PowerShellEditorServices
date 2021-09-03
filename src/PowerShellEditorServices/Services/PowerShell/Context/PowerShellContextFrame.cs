using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using System;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    internal class PowerShellContextFrame : IDisposable
    {
        public static PowerShellContextFrame CreateForPowerShellInstance(
            ILogger logger,
            SMA.PowerShell pwsh,
            PowerShellFrameType frameType,
            string localComputerName)
        {
            var runspaceInfo = RunspaceInfo.CreateFromPowerShell(logger, pwsh, localComputerName);
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PowerShell.Dispose();
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
    }
}
