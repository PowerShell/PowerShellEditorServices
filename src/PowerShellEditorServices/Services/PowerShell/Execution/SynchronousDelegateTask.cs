using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal class SynchronousDelegateTask : SynchronousTask<object>
    {
        private readonly Action<CancellationToken> _action;

        public SynchronousDelegateTask(
            ILogger logger,
            Action<CancellationToken> action,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _action = action;
        }

        public override object Run(CancellationToken cancellationToken)
        {
            _action(cancellationToken);
            return null;
        }
    }

    internal class SynchronousDelegateTask<TResult> : SynchronousTask<TResult>
    {
        private readonly Func<CancellationToken, TResult> _func;

        public SynchronousDelegateTask(
            ILogger logger,
            Func<CancellationToken, TResult> func,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _func = func;
        }

        public override TResult Run(CancellationToken cancellationToken)
        {
            return _func(cancellationToken);
        }
    }
}
