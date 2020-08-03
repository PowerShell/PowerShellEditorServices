using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PowerShellEditorServices.Services.PowerShell.Utility
{
    internal class AsyncResetEvent
    {
        private TaskCompletionSource<bool> _taskCompletionSource;

        public AsyncResetEvent()
        {
            _taskCompletionSource = null;
        }

        public bool IsBlocking => _taskCompletionSource != null;

        public void SetBlock()
        {
            Interlocked.CompareExchange(ref _taskCompletionSource, new TaskCompletionSource<bool>(), null);
        }

        public void Unblock()
        {
            TaskCompletionSource<bool> taskCompletionSource = Interlocked.Exchange(ref _taskCompletionSource, null);
            if (taskCompletionSource != null)
            {
                taskCompletionSource.TrySetResult(true);
            }
        }

        public Task WaitAsync()
        {
            if (_taskCompletionSource == null)
            {
                return Task.CompletedTask;
            }

            return _taskCompletionSource.Task;
        }
    }
}
