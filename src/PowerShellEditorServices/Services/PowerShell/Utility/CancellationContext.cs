﻿using System;
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
        private readonly ConcurrentStack<CancellationScope> _cancellationSourceStack;

        public CancellationContext()
        {
            _cancellationSourceStack = new ConcurrentStack<CancellationScope>();
        }

        public CancellationScope EnterScope(bool isIdleScope)
        {
            CancellationTokenSource newScopeCancellationSource = _cancellationSourceStack.TryPeek(out CancellationScope parentScope)
                ? CancellationTokenSource.CreateLinkedTokenSource(parentScope.CancellationToken)
                : new CancellationTokenSource();

            return EnterScope(isIdleScope, newScopeCancellationSource);
        }

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

        public void CancelIdleParentTask()
        {
            foreach (CancellationScope scope in _cancellationSourceStack)
            {
                if (!scope.IsIdleScope)
                {
                    break;
                }

                scope.Cancel();
            }
        }

        private CancellationScope EnterScope(bool isIdleScope, CancellationTokenSource cancellationFrameSource)
        {
            var scope = new CancellationScope(_cancellationSourceStack, cancellationFrameSource, isIdleScope);
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
            _cancellationStack.TryPop(out CancellationScope _);
            _cancellationSource.Cancel();
        }
    }
}