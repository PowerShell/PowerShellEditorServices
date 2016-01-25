//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Simplifies the setup of a SynchronizationContext for the use
    /// of async calls in the current thread.
    /// </summary>
    public static class AsyncContext
    {
        /// <summary>
        /// Starts a new ThreadSynchronizationContext, attaches it to
        /// the thread, and then runs the given async main function.
        /// </summary>
        /// <param name="asyncMainFunc">
        /// The Task-returning Func which represents the "main" function
        /// for the thread.
        /// </param>
        public static void Start(Func<Task> asyncMainFunc)
        {
            // Is there already a synchronization context?
            if (SynchronizationContext.Current != null)
            {
                throw new InvalidOperationException(
                    "A SynchronizationContext is already assigned on this thread.");
            }

            // Create and register a synchronization context for this thread
            var threadSyncContext = new ThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(threadSyncContext);

            // Get the main task and act on its completion
            Task asyncMainTask = asyncMainFunc();
            asyncMainTask.ContinueWith(
                t => threadSyncContext.EndLoop(),
                TaskScheduler.Default);

            // Start the synchronization context's request loop and
            // wait for the main task to complete
            threadSyncContext.RunLoopOnCurrentThread();
            asyncMainTask.GetAwaiter().GetResult();
        }
    }
}

