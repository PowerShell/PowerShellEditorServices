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
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Action<CancellationToken> action)
            : base(logger, cancellationToken)
        {
            ExecutionOptions = executionOptions;
            _representation = representation;
            _action = action;
        }

        public override ExecutionOptions ExecutionOptions { get; }

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
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Func<CancellationToken, TResult> func)
            : base(logger, cancellationToken)
        {
            _func = func;
            _representation = representation;
            ExecutionOptions = executionOptions;
        }

        public override ExecutionOptions ExecutionOptions { get; }

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
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Action<SMA.PowerShell, CancellationToken> action)
            : base(logger, cancellationToken)
        {
            _psesHost = psesHost;
            _action = action;
            _representation = representation;
            ExecutionOptions = executionOptions;
        }

        public override ExecutionOptions ExecutionOptions { get; }

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
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Func<SMA.PowerShell, CancellationToken, TResult> func)
            : base(logger, cancellationToken)
        {
            _psesHost = psesHost;
            _func = func;
            _representation = representation;
            ExecutionOptions = executionOptions;
        }

        public override ExecutionOptions ExecutionOptions { get; }

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
