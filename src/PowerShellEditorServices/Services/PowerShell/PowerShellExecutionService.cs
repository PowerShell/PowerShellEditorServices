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
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
        {
            return RunTaskAsync(new SynchronousPSDelegateTask<TResult>(_logger, _psesHost, func, representation, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            Action<SMA.PowerShell, CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
        {
            return RunTaskAsync(new SynchronousPSDelegateTask(_logger, _psesHost, action, representation, cancellationToken));
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            Func<CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
        {
            return RunTaskAsync(new SynchronousDelegateTask<TResult>(_logger, func, representation, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            Action<CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
        {
            return RunTaskAsync(new SynchronousDelegateTask(_logger, action, representation, cancellationToken));
        }

        public Task<IReadOnlyList<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken)
        {
            if (executionOptions.InterruptCommandPrompt)
            {
                return CancelCurrentAndRunTaskNowAsync(new SynchronousPowerShellTask<TResult>(
                    _logger,
                    _psesHost,
                    psCommand,
                    executionOptions,
                    cancellationToken));
            }

            return RunTaskAsync(new SynchronousPowerShellTask<TResult>(
                _logger,
                _psesHost,
                psCommand,
                executionOptions,
                cancellationToken));
        }

        public Task ExecutePSCommandAsync(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken) => ExecutePSCommandAsync<PSObject>(psCommand, executionOptions, cancellationToken);

        public void CancelCurrentTask()
        {
            _pipelineExecutor.CancelCurrentTask();
        }

        private Task<T> RunTaskAsync<T>(SynchronousTask<T> task) => _pipelineExecutor.RunTaskAsync(task);

        private Task<T> CancelCurrentAndRunTaskNowAsync<T>(SynchronousTask<T> task) => _pipelineExecutor.CancelCurrentAndRunTaskNowAsync(task);
    }
}
