using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal class PowerShellExecutor : ISynchronousExecutor
    {
        private readonly ILogger _logger;

        private readonly EditorServicesConsolePSHost _host;

        public PowerShellExecutor(
            ILoggerFactory loggerFactory,
            EditorServicesConsolePSHost host)
        {
            _logger = loggerFactory.CreateLogger<PowerShellExecutor>();
            _host = host;
        }

        public TResult InvokeDelegate<TResult>(string representation, ExecutionOptions executionOptions, Func<CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            var task = new SynchronousDelegateTask<TResult>(_logger, representation, executionOptions, cancellationToken, func);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokeDelegate(string representation, ExecutionOptions executionOptions, Action<CancellationToken> action, CancellationToken cancellationToken)
        {
            var task = new SynchronousDelegateTask(_logger, representation, executionOptions, cancellationToken, action);
            task.ExecuteAndGetResult(cancellationToken);
        }

        public IReadOnlyList<TResult> InvokePSCommand<TResult>(PSCommand psCommand, PowerShellExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            var task = new SynchronousPowerShellTask<TResult>(_logger, _host, psCommand, executionOptions, cancellationToken);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokePSCommand(PSCommand psCommand, PowerShellExecutionOptions executionOptions, CancellationToken cancellationToken)
            => InvokePSCommand<PSObject>(psCommand, executionOptions, cancellationToken);

        public TResult InvokePSDelegate<TResult>(string representation, ExecutionOptions executionOptions, Func<SMA.PowerShell, CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            var task = new SynchronousPSDelegateTask<TResult>(_logger, _host, representation, executionOptions, cancellationToken, func);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokePSDelegate(string representation, ExecutionOptions executionOptions, Action<SMA.PowerShell, CancellationToken> action, CancellationToken cancellationToken)
        {
            var task = new SynchronousPSDelegateTask(_logger, _host, representation, executionOptions, cancellationToken, action);
            task.ExecuteAndGetResult(cancellationToken);
        }
    }
}
