// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    /// <summary>
    /// Implements a concurrent deque that supplies:
    ///   - Non-blocking prepend and append operations
    ///   - Blocking and non-blocking take calls
    ///   - The ability to block consumers, so that <see cref="Prepend(T)"/> can also guarantee the state of the consumer
    /// </summary>
    /// <typeparam name="T">The type of item held by this collection.</typeparam>
    /// <remarks>
    /// The prepend/append semantics of this class depend on the implementation semantics of <see cref="BlockingCollection{T}.TryTakeFromAny(BlockingCollection{T}[], out T)"/>
    /// and its overloads checking the supplied array in order.
    /// This behavior is unlikely to change and ensuring its correctness at our layer is likely to be costly.
    /// See https://stackoverflow.com/q/26472251.
    /// </remarks>
    internal class BlockingConcurrentDeque<T> : IDisposable
    {
        private readonly ManualResetEventSlim _blockConsumersEvent;

        private readonly BlockingCollection<T>[] _queues;

        public BlockingConcurrentDeque()
        {
            // Initialize in the "set" state, meaning unblocked
            _blockConsumersEvent = new ManualResetEventSlim(initialState: true);

            _queues = new[]
            {
                // The high priority section is FIFO so that "prepend" always puts elements first
                new BlockingCollection<T>(new ConcurrentStack<T>()),
                new BlockingCollection<T>(new ConcurrentQueue<T>()),
            };
        }

        public bool IsEmpty => _queues[0].Count == 0 && _queues[1].Count == 0;

        public void Prepend(T item) => _queues[0].Add(item);

        public void Append(T item) => _queues[1].Add(item);

        public T Take(CancellationToken cancellationToken)
        {
            _blockConsumersEvent.Wait(cancellationToken);
            BlockingCollection<T>.TakeFromAny(_queues, out T result, cancellationToken);
            return result;
        }

        public bool TryTake(out T item)
        {
            if (!_blockConsumersEvent.IsSet)
            {
                item = default;
                return false;
            }

            return BlockingCollection<T>.TryTakeFromAny(_queues, out item) >= 0;
        }

        public IDisposable BlockConsumers() => PriorityQueueBlockLifetime.StartBlocking(_blockConsumersEvent);

        public void Dispose() => _blockConsumersEvent.Dispose();

        private class PriorityQueueBlockLifetime : IDisposable
        {
            public static PriorityQueueBlockLifetime StartBlocking(ManualResetEventSlim blockEvent)
            {
                blockEvent.Reset();
                return new PriorityQueueBlockLifetime(blockEvent);
            }

            private readonly ManualResetEventSlim _blockEvent;

            private PriorityQueueBlockLifetime(ManualResetEventSlim blockEvent) => _blockEvent = blockEvent;

            public void Dispose() => _blockEvent.Set();
        }
    }
}
