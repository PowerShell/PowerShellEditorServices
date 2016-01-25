//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Provides a SynchronizationContext implementation that can be used
    /// in console applications or any thread which doesn't have its
    /// own SynchronizationContext.
    /// </summary>
    public class ThreadSynchronizationContext : SynchronizationContext
    {
        #region Private Fields

        private BlockingCollection<Tuple<SendOrPostCallback, object>> requestQueue =
            new BlockingCollection<Tuple<SendOrPostCallback, object>>();

        #endregion

        #region Constructors

        /// <summary>
        /// Posts a request for execution to the SynchronizationContext.
        /// This will be executed on the SynchronizationContext's thread.
        /// </summary>
        /// <param name="callback">
        /// The callback to be invoked on the SynchronizationContext's thread.
        /// </param>
        /// <param name="state">
        /// A state object to pass along to the callback when executed through
        /// the SynchronizationContext.
        /// </param>
        public override void Post(SendOrPostCallback callback, object state)
        {
            // Add the request to the queue
            this.requestQueue.Add(
                new Tuple<SendOrPostCallback, object>(
                    callback, state));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the SynchronizationContext message loop on the current thread.
        /// </summary>
        public void RunLoopOnCurrentThread()
        {
            Tuple<SendOrPostCallback, object> request;

            while (this.requestQueue.TryTake(out request, Timeout.Infinite))
            {
                // Invoke the request's callback
                request.Item1(request.Item2);
            }
        }

        /// <summary>
        /// Ends the SynchronizationContext message loop.
        /// </summary>
        public void EndLoop()
        {
            // Tell the blocking queue that we're done
            this.requestQueue.CompleteAdding();
        }

        #endregion
    }
}

