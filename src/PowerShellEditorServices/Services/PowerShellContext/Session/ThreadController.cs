//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShellContext
{
    /// <summary>
    /// Provides the ability to route PowerShell command invocations to a specific thread.
    /// </summary>
    internal class ThreadController
    {
        private PromptNestFrame _nestFrame;

        internal AsyncQueue<IPipelineExecutionRequest> PipelineRequestQueue { get; }

        internal TaskCompletionSource<DebuggerResumeAction> FrameExitTask { get; }

        internal int ManagedThreadId { get; }

        internal bool IsPipelineThread { get; }

        /// <summary>
        /// Initializes an new instance of the ThreadController class. This constructor should only
        /// ever been called from the thread it is meant to control.
        /// </summary>
        /// <param name="nestFrame">The parent PromptNestFrame object.</param>
        internal ThreadController(PromptNestFrame nestFrame)
        {
            _nestFrame = nestFrame;
            PipelineRequestQueue = new AsyncQueue<IPipelineExecutionRequest>();
            FrameExitTask = new TaskCompletionSource<DebuggerResumeAction>();
            ManagedThreadId = Thread.CurrentThread.ManagedThreadId;

            // If the debugger stop is triggered on a thread with no default runspace we
            // shouldn't attempt to route commands to it.
            IsPipelineThread = Runspace.DefaultRunspace != null;
        }

        /// <summary>
        /// Determines if the caller is already on the thread that this object maintains.
        /// </summary>
        /// <returns>
        /// A value indicating if the caller is already on the thread maintained by this object.
        /// </returns>
        internal bool IsCurrentThread()
        {
            return Thread.CurrentThread.ManagedThreadId == ManagedThreadId;
        }

        /// <summary>
        /// Requests the invocation of a PowerShell command on the thread maintained by this object.
        /// </summary>
        /// <param name="executionRequest">The execution request to send.</param>
        /// <returns>
        /// A task object representing the asynchronous operation. The Result property will return
        /// the output of the command invocation.
        /// </returns>
        internal async Task<IEnumerable<TResult>> RequestPipelineExecutionAsync<TResult>(
            PipelineExecutionRequest<TResult> executionRequest)
        {
            await PipelineRequestQueue.EnqueueAsync(executionRequest).ConfigureAwait(false);
            return await executionRequest.Results.ConfigureAwait(false);
        }

        /// <summary>
        /// Retrieves the first currently queued execution request. If there are no pending
        /// execution requests then the task will be completed when one is requested.
        /// </summary>
        /// <returns>
        /// A task object representing the asynchronous operation. The Result property will return
        /// the retrieved pipeline execution request.
        /// </returns>
        internal Task<IPipelineExecutionRequest> TakeExecutionRequestAsync()
        {
            return PipelineRequestQueue.DequeueAsync();
        }

        /// <summary>
        /// Marks the thread to be exited.
        /// </summary>
        /// <param name="action">
        /// The resume action for the debugger. If the frame is not a debugger frame this parameter
        /// is ignored.
        /// </param>
        internal void StartThreadExit(DebuggerResumeAction action)
        {
            StartThreadExit(action, waitForExit: false);
        }

        /// <summary>
        /// Marks the thread to be exited.
        /// </summary>
        /// <param name="action">
        /// The resume action for the debugger. If the frame is not a debugger frame this parameter
        /// is ignored.
        /// </param>
        /// <param name="waitForExit">
        /// Indicates whether the method should block until the exit is completed.
        /// </param>
        internal void StartThreadExit(DebuggerResumeAction action, bool waitForExit)
        {
            Task.Run(() => FrameExitTask.TrySetResult(action));
            if (!waitForExit)
            {
                return;
            }

            _nestFrame.WaitForFrameExit(CancellationToken.None);
        }

        /// <summary>
        /// Creates a task object that completes when the thread has be marked for exit.
        /// </summary>
        /// <returns>
        /// A task object representing the frame receiving a request to exit. The Result property
        /// will return the DebuggerResumeAction supplied with the request.
        /// </returns>
        internal async Task<DebuggerResumeAction> Exit()
        {
            return await FrameExitTask.Task.ConfigureAwait(false);
        }
    }
}
