using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal interface ISynchronousTask
    {
        bool IsCanceled { get; }

        void ExecuteSynchronously(CancellationToken threadCancellationToken);
    }

    internal abstract class SynchronousTask<TResult> : ISynchronousTask
    {
        private readonly TaskCompletionSource<TResult> _taskCompletionSource;

        private readonly CancellationToken _taskRequesterCancellationToken;

        private bool _executionCanceled;

        protected SynchronousTask(ILogger logger, CancellationToken cancellationToken)
        {
            Logger = logger;
            _taskCompletionSource = new TaskCompletionSource<TResult>();
            _taskRequesterCancellationToken = cancellationToken;
            _executionCanceled = false;
        }

        protected ILogger Logger { get; }

        public Task<TResult> Task => _taskCompletionSource.Task;

        public bool IsCanceled => _executionCanceled || _taskRequesterCancellationToken.IsCancellationRequested;

        public abstract TResult Run(CancellationToken cancellationToken);

        public abstract override string ToString();

        public void ExecuteSynchronously(CancellationToken executorCancellationToken)
        {
            using (var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_taskRequesterCancellationToken, executorCancellationToken))
            {
                if (cancellationSource.IsCancellationRequested)
                {
                    SetCanceled();
                    return;
                }

                try
                {
                    TResult result = Run(cancellationSource.Token);

                    _taskCompletionSource.SetResult(result);
                }
                catch (OperationCanceledException)
                {
                    SetCanceled();
                }
                catch (Exception e)
                {
                    _taskCompletionSource.SetException(e);
                }
            }
        }

        private void SetCanceled()
        {
            _executionCanceled = true;
            _taskCompletionSource.SetCanceled();
        }
    }
}
