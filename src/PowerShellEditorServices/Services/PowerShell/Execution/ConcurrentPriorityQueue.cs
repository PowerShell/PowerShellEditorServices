using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PowerShellEditorServices.Services.PowerShell.Utility
{
    internal class ConcurrentPriorityQueue<T>
    {
        private readonly ConcurrentStack<T> _priorityItems;

        private readonly BlockingCollection<T> _queue;

        public ConcurrentPriorityQueue()
        {
            _priorityItems = new ConcurrentStack<T>();
            _queue = new BlockingCollection<T>();
        }

        public int Count => _priorityItems.Count + _queue.Count;

        public void Append(T item)
        {
            _queue.Add(item);
        }

        public void Prepend(T item)
        {
            _priorityItems.Push(item);
        }

        public virtual T Take(CancellationToken cancellationToken)
        {
            if (_priorityItems.TryPop(out T item))
            {
                return item;
            }

            return _queue.Take(cancellationToken);
        }

        public virtual bool TryTake(out T item)
        {
            if (_priorityItems.TryPop(out item))
            {
                return true;
            }

            return _queue.TryTake(out item);
        }
    }
}
