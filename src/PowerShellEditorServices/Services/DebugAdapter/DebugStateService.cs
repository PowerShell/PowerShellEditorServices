//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services
{
    internal class DebugStateService
    {
        private readonly SemaphoreSlim _setBreakpointInProgressHandle = AsyncUtils.CreateSimpleLockingSemaphore();

        internal bool NoDebug { get; set; }

        internal string Arguments { get; set; }

        internal bool IsRemoteAttach { get; set; }

        internal int? RunspaceId { get; set; }

        internal bool IsAttachSession { get; set; }

        internal bool WaitingForAttach { get; set; }

        internal string ScriptToLaunch { get; set; }

        internal bool OwnsEditorSession { get; set; }

        internal bool ExecutionCompleted { get; set; }

        internal bool IsInteractiveDebugSession { get; set; }

        // If the CurrentCount is equal to zero, then we have some thread using the handle.
        internal bool IsSetBreakpointInProgress => _setBreakpointInProgressHandle.CurrentCount == 0;

        internal bool IsUsingTempIntegratedConsole { get; set; }

        internal void ReleaseSetBreakpointHandle()
        {
            _setBreakpointInProgressHandle.Release();
        }

        internal async Task WaitForSetBreakpointHandleAsync()
        {
            await _setBreakpointInProgressHandle.WaitAsync()
                .ConfigureAwait(continueOnCapturedContext: false);
        }
    }
}
