// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal interface ISynchronousTask
    {
        bool IsCanceled { get; }

        void ExecuteSynchronously(CancellationToken threadCancellationToken);

        ExecutionOptions ExecutionOptions { get; }
    }

    internal abstract class SynchronousTask<TResult> : ISynchronousTask
    {
        private readonly TaskCompletionSource<TResult> _taskCompletionSource;

        private readonly CancellationToken _taskRequesterCancellationToken;

        private bool _executionCanceled;

        private TResult _result;

        private ExceptionDispatchInfo _exceptionInfo;

        protected SynchronousTask(
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Logger = logger;
            _taskCompletionSource = new TaskCompletionSource<TResult>();
            _taskRequesterCancellationToken = cancellationToken;
            _executionCanceled = false;
        }

        protected ILogger Logger { get; }

        public Task<TResult> Task => _taskCompletionSource.Task;

        // Sometimes we need the result of task run on the same thread,
        // which this property allows us to do.
        public TResult Result
        {
            get
            {
                if (_executionCanceled)
                {
                    throw new OperationCanceledException();
                }

                _exceptionInfo?.Throw();

                return _result;
            }
        }

        public bool IsCanceled => _executionCanceled || _taskRequesterCancellationToken.IsCancellationRequested;

        public abstract ExecutionOptions ExecutionOptions { get; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1158", Justification = "Field is not type-dependent")]
        internal static readonly ExecutionOptions s_defaultExecutionOptions = new();

        public abstract TResult Run(CancellationToken cancellationToken);

        public abstract override string ToString();

        public void ExecuteSynchronously(CancellationToken executorCancellationToken)
        {
            if (IsCanceled)
            {
                return;
            }

            using CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_taskRequesterCancellationToken, executorCancellationToken);
            if (cancellationSource.IsCancellationRequested)
            {
                SetCanceled();
                return;
            }

            try
            {
                TResult result = Run(cancellationSource.Token);
                SetResult(result);
            }
            catch (OperationCanceledException)
            {
                SetCanceled();
            }
            catch (Exception e)
            {
                SetException(e);
            }
        }

        public TResult ExecuteAndGetResult(CancellationToken cancellationToken)
        {
            ExecuteSynchronously(cancellationToken);
            return Result;
        }

        private void SetCanceled()
        {
            _executionCanceled = true;
            _taskCompletionSource.SetCanceled();
        }

        private void SetException(Exception e)
        {
            // We use this to capture the original stack trace so that exceptions will be useful later
            _exceptionInfo = ExceptionDispatchInfo.Capture(e);
            _taskCompletionSource.SetException(e);
        }

        private void SetResult(TResult result)
        {
            _result = result;
            _taskCompletionSource.SetResult(result);
        }
    }
}
