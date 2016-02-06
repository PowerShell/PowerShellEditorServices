//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Restricts the invocation of an operation to a specified time
    /// interval.  Can also cause previous requests to be cancelled
    /// by new requests within that time window.  Typically used for
    /// buffering information for an operation or ensuring that an
    /// operation only runs after some interval.
    /// </summary>
    /// <typeparam name="TInvokeArgs">The argument type for the Invoke method.</typeparam>
    public abstract class AsyncDebouncer<TInvokeArgs>
    {
        #region Private Fields

        private int flushInterval;
        private bool restartOnInvoke;

        private Task currentTimerTask;
        private CancellationTokenSource timerCancellationSource;

        private AsyncLock asyncLock = new AsyncLock();

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new instance of the AsyncDebouncer class with the
        /// specified flush interval.  If restartOnInvoke is true, any
        /// calls to Invoke will cancel previous calls which have not yet
        /// passed the flush interval.
        /// </summary>
        /// <param name="flushInterval">
        /// A millisecond interval to use for flushing prior Invoke calls.
        /// </param>
        /// <param name="restartOnInvoke">
        /// If true, Invoke calls will reset prior calls which haven't passed the flush interval.
        /// </param>
        public AsyncDebouncer(int flushInterval, bool restartOnInvoke)
        {
            this.flushInterval = flushInterval;
            this.restartOnInvoke = restartOnInvoke;
        }

        /// <summary>
        /// Invokes the debouncer with the given input.  The debouncer will
        /// wait for the specified interval before calling the Flush method
        /// to complete the operation.
        /// </summary>
        /// <param name="invokeArgument">
        /// The argument for this implementation's Invoke method.
        /// </param>
        /// <returns>A Task to be awaited until the Invoke is queued.</returns>
        public async Task Invoke(TInvokeArgs invokeArgument)
        {
            using (await this.asyncLock.LockAsync())
            {
                // Invoke the implementor
                await this.OnInvoke(invokeArgument);

                // If there's no timer, start one
                if (this.currentTimerTask == null)
                {
                    this.StartTimer();
                }
                else if (this.currentTimerTask != null && this.restartOnInvoke)
                {
                    // Restart the existing timer
                    if (this.CancelTimer())
                    {
                        this.StartTimer();
                    }
                }
            }
        }

        /// <summary>
        /// Flushes the latest state regardless of the current interval.
        /// An AsyncDebouncer MUST NOT invoke its own Flush method otherwise
        /// deadlocks could occur.
        /// </summary>
        /// <returns>A Task to be awaited until Flush completes.</returns>
        public async Task Flush()
        {
            using (await this.asyncLock.LockAsync())
            {
                // Cancel the current timer
                this.CancelTimer();

                // Flush the current output
                await this.OnFlush();
            }
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Implemented by the subclass to take the argument for the
        /// future operation that will be performed by OnFlush.
        /// </summary>
        /// <param name="invokeArgument">
        /// The argument for this implementation's OnInvoke method.
        /// </param>
        /// <returns>A Task to be awaited for the invoke to complete.</returns>
        protected abstract Task OnInvoke(TInvokeArgs invokeArgument);

        /// <summary>
        /// Implemented by the subclass to complete the current operation.
        /// </summary>
        /// <returns>A Task to be awaited for the operation to complete.</returns>
        protected abstract Task OnFlush();

        #endregion

        #region Private Methods

        private void StartTimer()
        {
            this.timerCancellationSource = new CancellationTokenSource();

            this.currentTimerTask =
                Task.Delay(this.flushInterval, this.timerCancellationSource.Token)
                    .ContinueWith(
                        t =>
                        {
                            if (!t.IsCanceled)
                            {
                                return this.Flush();
                            }
                            else
                            {
                                return Task.FromResult(true);
                            }
                        });
        }

        private bool CancelTimer()
        {
            if (this.timerCancellationSource != null)
            {
                // Attempt to cancel the timer task
                this.timerCancellationSource.Cancel();
            }

            // Was the task cancelled?
            bool wasCancelled = 
                this.currentTimerTask == null || 
                this.currentTimerTask.IsCanceled;

            // Clear the current task so that another may be created
            this.currentTimerTask = null;

            return wasCancelled;
        }

        #endregion
    }
}

