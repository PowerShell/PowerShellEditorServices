using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PowerShellEditorServices.Services.PowerShell.Utility
{
    internal class ConcurrentBlockablePriorityQueue<T>
    {
        private readonly object _priorityLock;

        private readonly ManualResetEventSlim _progressAllowedEvent;

        private readonly LinkedList<T> _priorityItems;

        private readonly BlockingCollection<T> _queue;

        public ConcurrentBlockablePriorityQueue()
        {
            _priorityLock = new object();
            _progressAllowedEvent = new ManualResetEventSlim();
            _priorityItems = new LinkedList<T>();
            _queue = new BlockingCollection<T>();
        }

        public int Count
        {
            get
            {
                lock (_priorityLock)
                {
                    return _priorityItems.Count + _queue.Count;
                }
            }
        }

        public void Enqueue(T item)
        {
            _queue.Add(item);
        }

        public void EnqueuePriority(T item)
        {
            lock (_priorityLock)
            {
                _priorityItems.AddLast(item);
            }
        }

        public void EnqueueNext(T item)
        {
            lock (_priorityLock)
            {
                _priorityItems.AddFirst(item);
            }
        }

        public T Take(CancellationToken cancellationToken)
        {
            _progressAllowedEvent.Wait();

            lock (_priorityLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_priorityItems.Count > 0)
                {
                    T item = _priorityItems.First.Value;
                    _priorityItems.RemoveFirst();
                    return item;
                }
            }

            return _queue.Take(cancellationToken);
        }

        public bool TryTake(out T item)
        {
            if (!_progressAllowedEvent.IsSet)
            {
                item = default;
                return false;
            }

            lock (_priorityLock)
            {
                if (_priorityItems.Count > 0)
                {
                    item = _priorityItems.First.Value;
                    _priorityItems.RemoveFirst();
                    return true;
                }
            }

            return _queue.TryTake(out item);
        }

        public IDisposable BlockConsumers()
        {
            return PriorityQueueBlockLifetime.StartBlock(_progressAllowedEvent);
        }

        private class PriorityQueueBlockLifetime : IDisposable
        {
            public static PriorityQueueBlockLifetime StartBlock(ManualResetEventSlim blockEvent)
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
