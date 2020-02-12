//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Utility
{
    public class AsyncQueueTests
    {
        [Fact]
        public async Task AsyncQueueSynchronizesAccess()
        {
            ConcurrentBag<int> outputItems = new ConcurrentBag<int>();
            AsyncQueue<int> inputQueue = new AsyncQueue<int>(Enumerable.Range(0, 100));
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            try
            {
                // Start 5 consumers
                await Task.WhenAll(
                    Task.Run(() => ConsumeItemsAsync(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(() => ConsumeItemsAsync(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(() => ConsumeItemsAsync(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(() => ConsumeItemsAsync(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(() => ConsumeItemsAsync(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(
                        async () =>
                        {
                            // Wait for a bit and then add more items to the queue
                            await Task.Delay(250);

                            foreach (var i in Enumerable.Range(100, 200))
                            {
                                await inputQueue.EnqueueAsync(i);
                            }

                            // Cancel the waiters
                            cancellationTokenSource.Cancel();
                        }));
            }
            catch (TaskCanceledException)
            {
                // Do nothing, this is expected.
            }

            // At this point, numbers 0 through 299 should be in the outputItems
            IEnumerable<int> expectedItems = Enumerable.Range(0, 300);
            Assert.Empty(expectedItems.Except(outputItems));
        }

        [Fact]
        public async Task AsyncQueueSkipsCancelledTasks()
        {
            AsyncQueue<int> inputQueue = new AsyncQueue<int>();

            // Queue up a couple of tasks to wait for input
            CancellationTokenSource cancellationSource = new CancellationTokenSource();
            Task<int> taskOne = inputQueue.DequeueAsync(cancellationSource.Token);
            Task<int> taskTwo = inputQueue.DequeueAsync();

            // Cancel the first task and then enqueue a number
            cancellationSource.Cancel();
            await inputQueue.EnqueueAsync(1);

            // Wait for things to propegate.
            await Task.Delay(1000).ConfigureAwait(false);

            // Did the second task get the number?
            Assert.Equal(TaskStatus.Canceled, taskOne.Status);
            Assert.Equal(TaskStatus.RanToCompletion, taskTwo.Status);
            Assert.Equal(1, taskTwo.Result);
        }

        private async Task ConsumeItemsAsync(
            AsyncQueue<int> inputQueue,
            ConcurrentBag<int> outputItems,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int consumedItem = await inputQueue.DequeueAsync(cancellationToken);
                outputItems.Add(consumedItem);
            }
        }
    }
}

