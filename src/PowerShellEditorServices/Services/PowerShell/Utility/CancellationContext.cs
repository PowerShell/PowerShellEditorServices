using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    internal class CancellationContext
    {
        private readonly ConcurrentStack<CancellationTokenSource> _cancellationSourceStack;

        public CancellationContext()
        {
            _cancellationSourceStack = new ConcurrentStack<CancellationTokenSource>();
        }

        public CancellationScope EnterScope(params CancellationToken[] linkedTokens)
        {
            return EnterScope(CancellationTokenSource.CreateLinkedTokenSource(linkedTokens));
        }

        public CancellationScope EnterScope(CancellationToken linkedToken1, CancellationToken linkedToken2)
        {
            return EnterScope(CancellationTokenSource.CreateLinkedTokenSource(linkedToken1, linkedToken2));
        }

        public void CancelCurrentTask()
        {
            if (_cancellationSourceStack.TryPeek(out CancellationTokenSource currentCancellationSource))
            {
                currentCancellationSource.Cancel();
            }
        }

        private CancellationScope EnterScope(CancellationTokenSource cancellationFrameSource)
        {
            _cancellationSourceStack.Push(cancellationFrameSource);
            return new CancellationScope(_cancellationSourceStack, cancellationFrameSource.Token);
        }
    }

    internal struct CancellationScope : IDisposable
    {
        private readonly ConcurrentStack<CancellationTokenSource> _cancellationStack;

        internal CancellationScope(ConcurrentStack<CancellationTokenSource> cancellationStack, CancellationToken currentCancellationToken)
        {
            _cancellationStack = cancellationStack;
            CancellationToken = currentCancellationToken;
        }

        public readonly CancellationToken CancellationToken;

        public void Dispose()
        {
            if (_cancellationStack.TryPop(out CancellationTokenSource contextCancellationTokenSource))
            {
                contextCancellationTokenSource.Dispose();
            }
        }
    }
}
