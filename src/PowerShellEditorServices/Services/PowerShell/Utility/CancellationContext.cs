using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PowerShellEditorServices.Services.PowerShell.Utility
{
    internal struct CancellationContext : IDisposable
    {
        public static CancellationContext Enter(ConcurrentStack<CancellationTokenSource> cancellationStack, params CancellationToken[] linkedTokens)
        {
            var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(linkedTokens);
            return Enter(cancellationStack, linkedCancellationSource);
        }

        public static CancellationContext Enter(ConcurrentStack<CancellationTokenSource> cancellationStack, CancellationToken token1, CancellationToken token2)
        {
            var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token1, token2);
            return Enter(cancellationStack, linkedCancellationSource);
        }

        private static CancellationContext Enter(ConcurrentStack<CancellationTokenSource> cancellationStack, CancellationTokenSource linkedCancellationSource)
        {
            cancellationStack.Push(linkedCancellationSource);
            return new CancellationContext(cancellationStack, linkedCancellationSource.Token);
        }

        private readonly ConcurrentStack<CancellationTokenSource> _cancellationStack;

        private CancellationContext(ConcurrentStack<CancellationTokenSource> cancellationStack, CancellationToken currentCancellationToken)
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
