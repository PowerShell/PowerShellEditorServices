using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal interface ISynchronousTask
    {
        bool IsCanceled { get; }

        void ExecuteSynchronously(ref CancellationTokenSource cancellationSource, CancellationToken threadCancellationToken);
    }

    internal abstract class SynchronousTask<TResult> : ISynchronousTask
    {
        private readonly TaskCompletionSource<TResult> _taskCompletionSource;

        private readonly CancellationToken _taskCancellationToken;

        private bool _executionCanceled;

        protected SynchronousTask(ILogger logger, CancellationToken cancellationToken)
        {
            Logger = logger;
            _taskCompletionSource = new TaskCompletionSource<TResult>();
            _taskCancellationToken = cancellationToken;
            _executionCanceled = false;
        }

        protected ILogger Logger { get; }

        public Task<TResult> Task => _taskCompletionSource.Task;

        public bool IsCanceled => _taskCancellationToken.IsCancellationRequested || _executionCanceled;

        public abstract TResult Run(CancellationToken cancellationToken);

        public abstract override string ToString();

        public void ExecuteSynchronously(ref CancellationTokenSource cancellationSource, CancellationToken threadCancellation)
        {
            if (_taskCancellationToken.IsCancellationRequested || threadCancellation.IsCancellationRequested)
            {
                Cancel();
                return;
            }

            cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_taskCancellationToken, threadCancellation);
            try
            {
                TResult result = Run(cancellationSource.Token);

                _taskCompletionSource.SetResult(result);
            }
            catch (OperationCanceledException)
            {
                Cancel();
            }
            catch (Exception e)
            {
                _taskCompletionSource.SetException(e);
            }
        }

        private void Cancel()
        {
            _executionCanceled = true;
            _taskCompletionSource.SetCanceled();
        }
    }
}
