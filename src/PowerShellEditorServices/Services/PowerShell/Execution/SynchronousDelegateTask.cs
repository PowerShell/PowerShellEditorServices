// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
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
            Action<CancellationToken> action,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            ExecutionOptions = executionOptions ?? s_defaultExecutionOptions;
            _representation = representation;
            _action = action;
        }

        public override ExecutionOptions ExecutionOptions { get; }

        public override object Run(CancellationToken cancellationToken)
        {
            _action(cancellationToken);
            return null;
        }

        public override string ToString() => _representation;
    }

    internal class SynchronousDelegateTask<TResult> : SynchronousTask<TResult>
    {
        private readonly Func<CancellationToken, TResult> _func;

        private readonly string _representation;

        public SynchronousDelegateTask(
            ILogger logger,
            string representation,
            ExecutionOptions executionOptions,
            Func<CancellationToken, TResult> func,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _func = func;
            _representation = representation;
            ExecutionOptions = executionOptions ?? s_defaultExecutionOptions;
        }

        public override ExecutionOptions ExecutionOptions { get; }

        public override TResult Run(CancellationToken cancellationToken) => _func(cancellationToken);

        public override string ToString() => _representation;
    }

    internal class SynchronousPSDelegateTask : SynchronousTask<object>
    {
        private readonly Action<SMA.PowerShell, CancellationToken> _action;

        private readonly string _representation;

        private readonly PsesInternalHost _psesHost;

        public SynchronousPSDelegateTask(
            ILogger logger,
            PsesInternalHost psesHost,
            string representation,
            ExecutionOptions executionOptions,
            Action<SMA.PowerShell, CancellationToken> action,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _psesHost = psesHost;
            _action = action;
            _representation = representation;
            ExecutionOptions = executionOptions ?? s_defaultExecutionOptions;
        }

        public override ExecutionOptions ExecutionOptions { get; }

        public override object Run(CancellationToken cancellationToken)
        {
            _action(_psesHost.CurrentPowerShell, cancellationToken);
            return null;
        }

        public override string ToString() => _representation;
    }

    internal class SynchronousPSDelegateTask<TResult> : SynchronousTask<TResult>
    {
        private readonly Func<SMA.PowerShell, CancellationToken, TResult> _func;

        private readonly string _representation;

        private readonly PsesInternalHost _psesHost;

        public SynchronousPSDelegateTask(
            ILogger logger,
            PsesInternalHost psesHost,
            string representation,
            ExecutionOptions executionOptions,
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            CancellationToken cancellationToken)
            : base(logger, cancellationToken)
        {
            _psesHost = psesHost;
            _func = func;
            _representation = representation;
            ExecutionOptions = executionOptions ?? s_defaultExecutionOptions;
        }

        public override ExecutionOptions ExecutionOptions { get; }

        public override TResult Run(CancellationToken cancellationToken) => _func(_psesHost.CurrentPowerShell, cancellationToken);

        public override string ToString() => _representation;
    }
}
