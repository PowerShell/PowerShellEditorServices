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
    /// Provides a simplified interface for creating a new thread
    /// and establishing an AsyncContext in it.
    /// </summary>
    public class AsyncContextThread
    {
        #region Private Fields

        private Task threadTask;
        private string threadName;
        private CancellationTokenSource threadCancellationToken =
            new CancellationTokenSource();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the AsyncContextThread class.
        /// </summary>
        /// <param name="threadName">
        /// The name of the thread for debugging purposes.
        /// </param>
        public AsyncContextThread(string threadName)
        {
            this.threadName = threadName;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Runs a task on the AsyncContextThread.
        /// </summary>
        /// <param name="taskReturningFunc">
        /// A Func which returns the task to be run on the thread.
        /// </param>
        /// <returns>
        /// A Task which can be used to monitor the thread for completion.
        /// </returns>
        public Task Run(Func<Task> taskReturningFunc)
        {
            // Start up a long-running task with the action as the
            // main entry point for the thread
            this.threadTask =
                Task.Factory.StartNew(
                    () =>
                    {
                        // Set the thread's name to help with debugging
                        Thread.CurrentThread.Name = "AsyncContextThread: " + this.threadName;

                        // Set up an AsyncContext to run the task
                        AsyncContext.Start(taskReturningFunc);
                    },
                    this.threadCancellationToken.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            return this.threadTask;
        }

        /// <summary>
        /// Stops the thread task.
        /// </summary>
        public void Stop()
        {
            this.threadCancellationToken.Cancel();
        }

        #endregion
    }
}

