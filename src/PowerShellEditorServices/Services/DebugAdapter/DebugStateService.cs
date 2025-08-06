// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class DebugStateService
    {
        private readonly SemaphoreSlim _setBreakpointInProgressHandle = AsyncUtils.CreateSimpleLockingSemaphore();
        private readonly SemaphoreSlim _inLaunchOrAttachHandle = AsyncUtils.CreateSimpleLockingSemaphore();
        private TaskCompletionSource<bool> _waitForConfigDone;

        internal bool NoDebug { get; set; }

        internal bool IsRemoteAttach { get; set; }

        internal int? RunspaceId { get; set; }

        internal bool IsAttachSession { get; set; }

        internal bool ExecutionCompleted { get; set; }

        internal bool IsInteractiveDebugSession { get; set; }

        // If the CurrentCount is equal to zero, then we have some thread using the handle.
        internal bool IsSetBreakpointInProgress => _setBreakpointInProgressHandle.CurrentCount == 0;

        internal bool IsUsingTempIntegratedConsole { get; set; }

        // This gets set at the end of the Launch/Attach handler which set debug state.
        internal TaskCompletionSource<bool> ServerStarted { get; set; }

        internal int ReleaseSetBreakpointHandle() => _setBreakpointInProgressHandle.Release();

        internal Task WaitForSetBreakpointHandleAsync() => _setBreakpointInProgressHandle.WaitAsync();

        /// <summary>
        /// Sends the InitializedEvent and waits for the configuration done
        /// event to be sent by the client.
        /// </summary>
        /// <param name="action">The action being performed, either "attach" or "launch".</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="RpcErrorException">A launch or attach request is already in progress</exception>
        internal async Task WaitForConfigurationDoneAsync(
            string action,
            CancellationToken cancellationToken)
        {
            Task<bool> waitForConfigDone;

            // First check we are not already running a launch or attach request.
            await _inLaunchOrAttachHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_waitForConfigDone is not null)
                {
                    // If we are already waiting for a configuration done, then we cannot start another
                    // launch or attach request.
                    throw new RpcErrorException(0, null, $"Cannot start a new {action} request when one is already in progress.");
                }

                _waitForConfigDone = new TaskCompletionSource<bool>();
                waitForConfigDone = _waitForConfigDone.Task;
            }
            finally
            {
                _inLaunchOrAttachHandle.Release();
            }

            using CancellationTokenRegistration _ = cancellationToken.Register(_waitForConfigDone.SetCanceled);

            // Sends the InitializedEvent so that the debugger will continue
            // sending configuration requests before the final configuration
            // done.
            ServerStarted.TrySetResult(true);
            await waitForConfigDone.ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the configuration done task to complete, indicating that the
        /// client has sent all the initial configuration information and the
        /// debugger is ready to start.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        internal async Task SetConfigurationDoneAsync(
            CancellationToken cancellationToken)
        {
            await _inLaunchOrAttachHandle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_waitForConfigDone is null)
                {
                    // If we are not waiting for a configuration done, then we cannot set it.
                    throw new RpcErrorException(0, null, "Unexpected configuration done request when server is not expecting it.");
                }

                _waitForConfigDone.TrySetResult(true);
                _waitForConfigDone = null;
            }
            finally
            {
                _inLaunchOrAttachHandle.Release();
            }
        }
    }
}
