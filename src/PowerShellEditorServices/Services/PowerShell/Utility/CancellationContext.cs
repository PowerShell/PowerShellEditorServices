// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    /// The <see cref="CancellationContext"/> class
    /// and the <see cref="CancellationScope"/> struct
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
        private readonly ConcurrentStack<CancellationScope> _cancellationSourceStack;

        public CancellationContext() => _cancellationSourceStack = new ConcurrentStack<CancellationScope>();

        public CancellationScope EnterScope(bool isIdleScope, CancellationToken cancellationToken)
        {
            CancellationTokenSource newScopeCancellationSource = _cancellationSourceStack.TryPeek(out CancellationScope parentScope)
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, parentScope.CancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            return EnterScope(isIdleScope, newScopeCancellationSource);
        }

        public CancellationScope EnterScope(bool isIdleScope) => EnterScope(isIdleScope, CancellationToken.None);

        public void CancelCurrentTask()
        {
            if (_cancellationSourceStack.TryPeek(out CancellationScope currentCancellationSource))
            {
                currentCancellationSource.Cancel();
            }
        }

        public void CancelCurrentTaskStack()
        {
            foreach (CancellationScope scope in _cancellationSourceStack)
            {
                scope.Cancel();
            }
        }

        /// <summary>
        /// Cancels the parent task of the idle task.
        /// </summary>
        public void CancelIdleParentTask()
        {
            foreach (CancellationScope scope in _cancellationSourceStack)
            {
                scope.Cancel();

                // Note that this check is done *after* the cancellation because we want to cancel
                // not just the idle task, but its parent as well
                // because we want to cancel the ReadLine call that the idle handler is running in
                // so we can run something else in the foreground
                if (!scope.IsIdleScope)
                {
                    break;
                }
            }
        }

        private CancellationScope EnterScope(bool isIdleScope, CancellationTokenSource cancellationFrameSource)
        {
            CancellationScope scope = new(_cancellationSourceStack, cancellationFrameSource, isIdleScope);
            _cancellationSourceStack.Push(scope);
            return scope;
        }
    }

    internal class CancellationScope : IDisposable
    {
        private readonly ConcurrentStack<CancellationScope> _cancellationStack;

        private readonly CancellationTokenSource _cancellationSource;

        internal CancellationScope(
            ConcurrentStack<CancellationScope> cancellationStack,
            CancellationTokenSource frameCancellationSource,
            bool isIdleScope)
        {
            _cancellationStack = cancellationStack;
            _cancellationSource = frameCancellationSource;
            IsIdleScope = isIdleScope;
        }

        public CancellationToken CancellationToken => _cancellationSource.Token;

        public void Cancel() => _cancellationSource.Cancel();

        public bool IsIdleScope { get; }

        public void Dispose()
        {
            // TODO: This is whack. It used to call `Cancel` on the cancellation source, but we
            // shouldn't do that!
            _cancellationSource.Dispose();
            _cancellationStack.TryPop(out CancellationScope _);
        }
    }
}
