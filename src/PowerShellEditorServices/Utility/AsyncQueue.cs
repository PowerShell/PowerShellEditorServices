//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// Provides a synchronized queue which can be used from within async
    /// operations.  This is primarily used for producer/consumer scenarios.
    /// </summary>
    /// <typeparam name="T">The type of item contained in the queue.</typeparam>
    internal class AsyncQueue<T>
    {
        #region Private Fields

        private AsyncLock queueLock = new AsyncLock();
        private Queue<T> itemQueue;
        private Queue<TaskCompletionSource<T>> requestQueue;

        #endregion

        #region Properties

        /// <summary>
        /// Returns true if the queue is currently empty.
        /// </summary>
        public bool IsEmpty { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes an empty instance of the AsyncQueue class.
        /// </summary>
        public AsyncQueue() : this(Enumerable.Empty<T>())
        {
        }

        /// <summary>
        /// Initializes an instance of the AsyncQueue class, pre-populated
        /// with the given collection of items.
        /// </summary>
        /// <param name="initialItems">
        /// An IEnumerable containing the initial items with which the queue will
        /// be populated.
        /// </param>
        public AsyncQueue(IEnumerable<T> initialItems)
        {
            this.itemQueue = new Queue<T>(initialItems);
            this.requestQueue = new Queue<TaskCompletionSource<T>>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Enqueues an item onto the end of the queue.
        /// </summary>
        /// <param name="item">The item to be added to the queue.</param>
        /// <returns>
        /// A Task which can be awaited until the synchronized enqueue
        /// operation completes.
        /// </returns>
        public async Task EnqueueAsync(T item)
        {
            using (await queueLock.LockAsync().ConfigureAwait(false))
            {
                TaskCompletionSource<T> requestTaskSource = null;

                // Are any requests waiting?
                while (this.requestQueue.Count > 0)
                {
                    // Is the next request cancelled already?
                    requestTaskSource = this.requestQueue.Dequeue();
                    if (!requestTaskSource.Task.IsCanceled)
                    {
                        // Dispatch the item
                        requestTaskSource.SetResult(item);
                        return;
                    }
                }

                // No more requests waiting, queue the item for a later request
                this.itemQueue.Enqueue(item);
                this.IsEmpty = false;
            }
        }

        /// <summary>
        /// Enqueues an item onto the end of the queue.
        /// </summary>
        /// <param name="item">The item to be added to the queue.</param>
        public void Enqueue(T item)
        {
            using (queueLock.Lock())
            {
                while (this.requestQueue.Count > 0)
                {
                    var requestTaskSource = this.requestQueue.Dequeue();
                    if (requestTaskSource.Task.IsCanceled)
                    {
                        continue;
                    }

                    requestTaskSource.SetResult(item);
                    return;
                }
            }

            this.itemQueue.Enqueue(item);
            this.IsEmpty = false;
        }

        /// <summary>
        /// Dequeues an item from the queue or waits asynchronously
        /// until an item is available.
        /// </summary>
        /// <returns>
        /// A Task which can be awaited until a value can be dequeued.
        /// </returns>
        public Task<T> DequeueAsync()
        {
            return this.DequeueAsync(CancellationToken.None);
        }

        /// <summary>
        /// Dequeues an item from the queue or waits asynchronously
        /// until an item is available.  The wait can be cancelled
        /// using the given CancellationToken.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken with which a dequeue wait can be cancelled.
        /// </param>
        /// <returns>
        /// A Task which can be awaited until a value can be dequeued.
        /// </returns>
        public async Task<T> DequeueAsync(CancellationToken cancellationToken)
        {
            Task<T> requestTask;

            using (await queueLock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                if (this.itemQueue.Count > 0)
                {
                    // Items are waiting to be taken so take one immediately
                    T item = this.itemQueue.Dequeue();
                    this.IsEmpty = this.itemQueue.Count == 0;

                    return item;
                }
                else
                {
                    // Queue the request for the next item
                    var requestTaskSource = new TaskCompletionSource<T>();
                    this.requestQueue.Enqueue(requestTaskSource);

                    // Register the wait task for cancel notifications
                    cancellationToken.Register(
                        () => requestTaskSource.TrySetCanceled());

                    requestTask = requestTaskSource.Task;
                }
            }

            // Wait for the request task to complete outside of the lock
            return await requestTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Dequeues an item from the queue or waits asynchronously
        /// until an item is available.
        /// </summary>
        /// <returns></returns>
        public T Dequeue()
        {
            return Dequeue(CancellationToken.None);
        }

        /// <summary>
        /// Dequeues an item from the queue or waits asynchronously
        /// until an item is available.  The wait can be cancelled
        /// using the given CancellationToken.
        /// </summary>
        /// <param name="cancellationToken">
        /// A CancellationToken with which a dequeue wait can be cancelled.
        /// </param>
        /// <returns></returns>
        public T Dequeue(CancellationToken cancellationToken)
        {
            TaskCompletionSource<T> requestTask;
            using (queueLock.Lock(cancellationToken))
            {
                if (this.itemQueue.Count > 0)
                {
                    T item = this.itemQueue.Dequeue();
                    this.IsEmpty = this.itemQueue.Count == 0;

                    return item;
                }

                requestTask = new TaskCompletionSource<T>();
                this.requestQueue.Enqueue(requestTask);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(() => requestTask.TrySetCanceled());
                }
            }

            return requestTask.Task.GetAwaiter().GetResult();
        }

        #endregion
    }
}
