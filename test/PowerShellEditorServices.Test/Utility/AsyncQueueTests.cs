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
                    Task.Run(() => ConsumeItems(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(() => ConsumeItems(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(() => ConsumeItems(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(() => ConsumeItems(inputQueue, outputItems, cancellationTokenSource.Token)),
                    Task.Run(() => ConsumeItems(inputQueue, outputItems, cancellationTokenSource.Token)),
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
            Assert.Equal(0, expectedItems.Except(outputItems).Count());
        }

        private async Task ConsumeItems(
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

