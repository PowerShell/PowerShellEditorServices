using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    internal class PowerShellExecutionService
    {
        private readonly ILogger _logger;

        private readonly EditorServicesConsolePSHost _psesHost;

        private readonly PipelineThreadExecutor _pipelineExecutor;

        public PowerShellExecutionService(
            ILoggerFactory loggerFactory,
            EditorServicesConsolePSHost psesHost,
            PipelineThreadExecutor pipelineExecutor)
        {
            _logger = loggerFactory.CreateLogger<PowerShellExecutionService>();
            _psesHost = psesHost;
            _pipelineExecutor = pipelineExecutor;
        }

        public Action<object, RunspaceChangedEventArgs> RunspaceChanged;

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Func<SMA.PowerShell, CancellationToken, TResult> func)
        {
            return RunTaskAsync(new SynchronousPSDelegateTask<TResult>(_logger, _psesHost, representation, executionOptions ?? ExecutionOptions.Default, cancellationToken, func));
        }

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Action<SMA.PowerShell, CancellationToken> action)
        {
            return RunTaskAsync(new SynchronousPSDelegateTask(_logger, _psesHost, representation, executionOptions ?? ExecutionOptions.Default, cancellationToken, action));
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Func<CancellationToken, TResult> func)
        {
            return RunTaskAsync(new SynchronousDelegateTask<TResult>(_logger, representation, executionOptions ?? ExecutionOptions.Default, cancellationToken, func));
        }

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Action<CancellationToken> action)
        {
            return RunTaskAsync(new SynchronousDelegateTask(_logger, representation, executionOptions ?? ExecutionOptions.Default, cancellationToken, action));
        }

        public Task<IReadOnlyList<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null)
        {
            return RunTaskAsync(new SynchronousPowerShellTask<TResult>(
                _logger,
                _psesHost,
                psCommand,
                executionOptions ?? PowerShellExecutionOptions.Default,
                cancellationToken));
        }

        public Task ExecutePSCommandAsync(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null) => ExecutePSCommandAsync<PSObject>(psCommand, cancellationToken, executionOptions);

        public void CancelCurrentTask()
        {
            _pipelineExecutor.CancelCurrentTask();
        }

        private Task<T> RunTaskAsync<T>(SynchronousTask<T> task) => _pipelineExecutor.RunTaskAsync(task);
    }
}
