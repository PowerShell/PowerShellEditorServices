using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using System;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    internal class PowerShellContextFrame : IDisposable
    {
        private bool disposedValue;

        public PowerShellContextFrame(SMA.PowerShell powerShell, PowerShellFrameType frameType, CancellationTokenSource cancellationTokenSource)
        {
            PowerShell = powerShell;
            FrameType = frameType;
            CancellationTokenSource = cancellationTokenSource;
        }

        public SMA.PowerShell PowerShell { get; }

        public PowerShellFrameType FrameType { get; }

        public CancellationTokenSource CancellationTokenSource { get; }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PowerShell.Dispose();
                    CancellationTokenSource.Dispose();
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
