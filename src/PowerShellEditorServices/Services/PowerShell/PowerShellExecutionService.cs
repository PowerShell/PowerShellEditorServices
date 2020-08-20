using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
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
    internal class PowerShellExecutionService : IDisposable
    {
        public static PowerShellExecutionService CreateAndStart(
            ILoggerFactory loggerFactory,
            ILanguageServer languageServer,
            HostStartupInfo hostInfo)
        {
            var executionService = new PowerShellExecutionService(
                loggerFactory,
                hostInfo,
                languageServer);

            executionService._pipelineExecutor.Start(executionService._pwshContext, executionService._consoleRepl);
            executionService._consoleRepl?.StartRepl();

            return executionService;
        }

        private readonly ILogger _logger;

        private readonly PowerShellContext _pwshContext;

        private readonly PipelineThreadExecutor _pipelineExecutor;

        private readonly ConsoleReplRunner _consoleRepl;

        private PowerShellExecutionService(
            ILoggerFactory loggerFactory,
            HostStartupInfo hostInfo,
            ILanguageServer languageServer)
        {
            _logger = loggerFactory.CreateLogger<PowerShellExecutionService>();
            _pipelineExecutor = new PipelineThreadExecutor(loggerFactory, hostInfo);
            _pwshContext = new PowerShellContext(loggerFactory, hostInfo, languageServer, this, _pipelineExecutor);

            // TODO: Fix this
            if (hostInfo.ConsoleReplEnabled)
            {
                _consoleRepl = new ConsoleReplRunner(loggerFactory, _pwshContext, this);
            }
        }

        public IPowerShellDebugContext DebugContext => _pwshContext.DebugContext;

        public IRunspaceInfo CurrentRunspace => _pwshContext.RunspaceContext;

        public IPowerShellContext PowerShellContext => _pwshContext;

        public Action<object, RunspaceChangedEventArgs> RunspaceChanged;

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
        {
            return QueueTask(new SynchronousPSDelegateTask<TResult>(_logger, _pwshContext, func, representation, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            Action<SMA.PowerShell, CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
        {
            return QueueTask(new SynchronousPSDelegateTask(_logger, _pwshContext, action, representation, cancellationToken));
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            Func<CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
        {
            return QueueTask(new SynchronousDelegateTask<TResult>(_logger, func, representation, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            Action<CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
        {
            return QueueTask(new SynchronousDelegateTask(_logger, action, representation, cancellationToken));
        }

        public Task<IReadOnlyList<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken)
        {
            Task<IReadOnlyList<TResult>> result = QueueTask(new SynchronousPowerShellTask<TResult>(
                _logger,
                _pwshContext,
                psCommand,
                executionOptions,
                cancellationToken));

            if (executionOptions.InterruptCommandPrompt)
            {
                _consoleRepl.CancelCurrentPrompt();
            }

            return result;
        }

        public Task ExecutePSCommandAsync(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken) => ExecutePSCommandAsync<PSObject>(psCommand, executionOptions, cancellationToken);

        public void CancelCurrentTask()
        {
            _pipelineExecutor.CancelCurrentTask();
        }

        public void Dispose()
        {
            _pipelineExecutor.Dispose();
            _consoleRepl.Dispose();
            _pwshContext.Dispose();
        }

        private Task<T> QueueTask<T>(SynchronousTask<T> task) => _pipelineExecutor.QueueTask(task);
    }
}
