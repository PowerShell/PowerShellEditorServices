using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
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

        private readonly EditorServicesConsolePSHost _psesHost;

        public SynchronousPSDelegateTask(
            ILogger logger,
            EditorServicesConsolePSHost psesHost,
            Action<SMA.PowerShell, CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _psesHost = psesHost;
            _action = action;
            _representation = representation;
        }

        public override object Run(CancellationToken cancellationToken)
        {
            _action(_psesHost.CurrentPowerShell, cancellationToken);
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

        private readonly EditorServicesConsolePSHost _psesHost;

        public SynchronousPSDelegateTask(
            ILogger logger,
            EditorServicesConsolePSHost psesHost,
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _psesHost = psesHost;
            _func = func;
            _representation = representation;
        }

        public override TResult Run(CancellationToken cancellationToken)
        {
            return _func(_psesHost.CurrentPowerShell, cancellationToken);
        }

        public override string ToString()
        {
            return _representation;
        }
    }
}
