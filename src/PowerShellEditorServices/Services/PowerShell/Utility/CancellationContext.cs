using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility
{
    /// <summary>
    /// Encapsulates the scoping logic for cancellation tokens.
    /// As PowerShell commands nest, this class maintains a stack of cancellation scopes
    /// that allow each scope of logic to be cancelled at its own level.
    /// Implicitly handles the merging and cleanup of cancellation token sources.
    /// </summary>
    /// <example>
    /// The <see cref="Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility.CancellationContext"/> class
    /// and the <see cref="Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility.CancellationScope"/> struct
    /// are intended to be used with a <c>using</c> block so you can do this:
    /// <code>
    ///     using (CancellationScope cancellationScope = _cancellationContext.EnterScope(_globalCancellationSource.CancellationToken, localCancellationToken))
    ///     {
    ///         ExecuteCommandAsync(command, cancellationScope.CancellationToken);
    ///     }
    /// </code>
    /// </example>
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

        public void CancelCurrentTaskStack()
        {
            foreach (CancellationTokenSource cancellationSource in _cancellationSourceStack)
            {
                cancellationSource.Cancel();
            }
        }

        private CancellationScope EnterScope(CancellationTokenSource cancellationFrameSource)
        {
            _cancellationSourceStack.Push(cancellationFrameSource);
            return new CancellationScope(_cancellationSourceStack, cancellationFrameSource.Token);
        }
    }

    internal class CancellationScope : IDisposable
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
