//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    using System.Management.Automation;

    /// <summary>
    /// Represents a single frame in the <see cref="PromptNest" />.
    /// </summary>
    internal class PromptNestFrame : IDisposable
    {
        private const PSInvocationState IndisposableStates = PSInvocationState.Stopping | PSInvocationState.Running;

        private SemaphoreSlim _frameExited = new SemaphoreSlim(initialCount: 0);

        private bool _isDisposed = false;

        /// <summary>
        /// Gets the <see cref="PowerShell" /> instance.
        /// </summary>
        internal PowerShell PowerShell { get; }

        /// <summary>
        /// Gets the <see cref="RunspaceHandle" /> queue that controls command invocation order.
        /// </summary>
        internal AsyncQueue<RunspaceHandle> Queue { get; }

        /// <summary>
        /// Gets the frame type.
        /// </summary>
        internal PromptNestFrameType FrameType { get; }

        /// <summary>
        /// Gets the <see cref="ThreadController" />.
        /// </summary>
        internal ThreadController ThreadController { get; }

        /// <summary>
        /// Gets a value indicating whether the frame requires command invocations
        /// to be routed to a specific thread.
        /// </summary>
        internal bool IsThreadController { get; }

        internal PromptNestFrame(PowerShell powerShell, AsyncQueue<RunspaceHandle> handleQueue)
            : this(powerShell, handleQueue, PromptNestFrameType.Normal)
            { }

        internal PromptNestFrame(
            PowerShell powerShell,
            AsyncQueue<RunspaceHandle> handleQueue,
            PromptNestFrameType frameType)
        {
            PowerShell = powerShell;
            Queue = handleQueue;
            FrameType = frameType;
            IsThreadController = (frameType & (PromptNestFrameType.Debug | PromptNestFrameType.NestedPrompt)) != 0;
            if (!IsThreadController)
            {
                return;
            }

            ThreadController = new ThreadController(this);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                if (IndisposableStates.HasFlag(PowerShell.InvocationStateInfo.State))
                {
                    PowerShell.BeginStop(
                        asyncResult =>
                        {
                            PowerShell.Runspace = null;
                            PowerShell.Dispose();
                        },
                        state: null);
                }
                else
                {
                    PowerShell.Runspace = null;
                    PowerShell.Dispose();
                }

                _frameExited.Release();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Blocks until the frame has been disposed.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> that will exit the block when cancelled.
        /// </param>
        internal void WaitForFrameExit(CancellationToken cancellationToken)
        {
            _frameExited.Wait(cancellationToken);
            _frameExited.Release();
        }

        /// <summary>
        /// Creates a task object that is completed when the frame has been disposed.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> that will be checked prior to completing
        /// the returned task.
        /// </param>
        /// <returns>
        /// A <see cref="Task" /> object that represents this frame being disposed.
        /// </returns>
        internal async Task WaitForFrameExitAsync(CancellationToken cancellationToken)
        {
            await _frameExited.WaitAsync(cancellationToken).ConfigureAwait(false);
            _frameExited.Release();
        }
    }
}
