using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal class SynchronousDelegateTask : SynchronousTask<object>
    {
        private readonly Action<CancellationToken> _action;

        private readonly string _representation;

        public SynchronousDelegateTask(
            ILogger logger,
            Action<CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _action = action;
            _representation = representation;
        }

        public override object Run(CancellationToken cancellationToken)
        {
            _action(cancellationToken);
            return null;
        }

        public override string ToString()
        {
            return _representation;
        }
    }

    internal class SynchronousDelegateTask<TResult> : SynchronousTask<TResult>
    {
        private readonly Func<CancellationToken, TResult> _func;

        private readonly string _representation;

        public SynchronousDelegateTask(
            ILogger logger,
            Func<CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _func = func;
            _representation = representation;
        }

        public override TResult Run(CancellationToken cancellationToken)
        {
            return _func(cancellationToken);
        }

        public override string ToString()
        {
            return _representation;
        }
    }

    internal class SynchronousPSDelegateTask : SynchronousTask<object>
    {
        private readonly Action<SMA.PowerShell, CancellationToken> _action;

        private readonly string _representation;

        private readonly PowerShellExecutionService.PowerShellRunspaceContext _psRunspaceContext;

        public SynchronousPSDelegateTask(
            ILogger logger,
            PowerShellExecutionService.PowerShellRunspaceContext psRunspaceContext,
            Action<SMA.PowerShell, CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _psRunspaceContext = psRunspaceContext;
            _action = action;
            _representation = representation;
        }

        public override object Run(CancellationToken cancellationToken)
        {
            _action(_psRunspaceContext.CurrentPowerShell, cancellationToken);
            return null;
        }

        public override string ToString()
        {
            return _representation;
        }
    }

    internal class SynchronousPSDelegateTask<TResult> : SynchronousTask<TResult>
    {
        private readonly Func<SMA.PowerShell, CancellationToken, TResult> _func;

        private readonly string _representation;

        private readonly PowerShellExecutionService.PowerShellRunspaceContext _psRunspaceContext;

        public SynchronousPSDelegateTask(
            ILogger logger,
            PowerShellExecutionService.PowerShellRunspaceContext psRunspaceContext,
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _psRunspaceContext = psRunspaceContext;
            _func = func;
            _representation = representation;
        }

        public override TResult Run(CancellationToken cancellationToken)
        {
            return _func(_psRunspaceContext.CurrentPowerShell, cancellationToken);
        }

        public override string ToString()
        {
            return _representation;
        }
    }
}
