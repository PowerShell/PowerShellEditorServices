using System;
using System.Threading;

namespace PowerShellEditorServices.Services.PowerShell.Utility
{
    internal class BlockableConcurrentPriorityQueue<T> : ConcurrentPriorityQueue<T>
    {
        private readonly ManualResetEventSlim _progressAllowedEvent;

        public BlockableConcurrentPriorityQueue()
            : base()
        {
            // Start the reset event in the set state, meaning not blocked
            _progressAllowedEvent = new ManualResetEventSlim(initialState: true);
        }

        public IDisposable BlockConsumers()
        {
            return PriorityQueueBlockLifetime.StartBlocking(_progressAllowedEvent);
        }

        public override T Take(CancellationToken cancellationToken)
        {
            _progressAllowedEvent.Wait();

            return base.Take(cancellationToken);
        }

        public override bool TryTake(out T item)
        {
            if (!_progressAllowedEvent.IsSet)
            {
                item = default;
                return false;
            }

            return base.TryTake(out item);
        }

        private class PriorityQueueBlockLifetime : IDisposable
        {
            public static PriorityQueueBlockLifetime StartBlocking(ManualResetEventSlim blockEvent)
            {
                blockEvent.Reset();
                return new PriorityQueueBlockLifetime(blockEvent);
            }

            private readonly ManualResetEventSlim _blockEvent;

            private PriorityQueueBlockLifetime(ManualResetEventSlim blockEvent)
            {
                _blockEvent = blockEvent;
            }

            public void Dispose()
            {
                _blockEvent.Set();
            }
        }
    }
}
