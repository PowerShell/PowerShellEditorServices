using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    internal class PowerShellExecutionService : IDisposable
    {
        private static readonly string s_commandsModulePath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "../../Commands/PowerShellEditorServices.Commands.psd1"));

        private readonly SMA.PowerShell _pwsh;

        private readonly CancellationTokenSource _stopThreadCancellationSource;

        private readonly BlockingCollection<ISynchronousTask> _executionQueue;

        private Thread _pipelineThread;

        private CancellationTokenSource _currentExecutionCancellationSource;

        private ILogger _logger;

        public static PowerShellExecutionService CreateAndStart(
            ILogger logger,
            HostStartupInfo hostInfo,
            PowerShellStartupService startupService)
        {
            var executionService = new PowerShellExecutionService(logger, startupService.PowerShell);

            executionService.Start();

            startupService.ReadLine.RegisterExecutionService(executionService);

            executionService.EnqueueModuleImport(s_commandsModulePath);

            if (hostInfo.AdditionalModules != null && hostInfo.AdditionalModules.Count > 0)
            {
                foreach (string module in hostInfo.AdditionalModules)
                {
                    executionService.EnqueueModuleImport(module);
                }
            }

            return executionService;
        }

        private PowerShellExecutionService(
            ILogger logger,
            SMA.PowerShell pwsh)
        {
            _logger = logger;
            _pwsh = pwsh;
            _stopThreadCancellationSource = new CancellationTokenSource();
            _executionQueue = new BlockingCollection<ISynchronousTask>();
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(Func<SMA.PowerShell, CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            TResult appliedFunc(CancellationToken cancellationToken) => func(_pwsh, cancellationToken);
            return ExecuteDelegateAsync(appliedFunc, cancellationToken);
        }

        public Task ExecuteDelegateAsync(Action<SMA.PowerShell, CancellationToken> action, CancellationToken cancellationToken)
        {
            void appliedAction(CancellationToken cancellationToken) => action(_pwsh, cancellationToken);
            return ExecuteDelegateAsync(appliedAction, cancellationToken);
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(Func<CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            var delegateTask = new SynchronousDelegateTask<TResult>(_logger, func, cancellationToken);
            _executionQueue.Add(delegateTask);
            return delegateTask.Task;
        }

        public Task ExecuteDelegateAsync(Action<CancellationToken> action, CancellationToken cancellationToken)
        {
            var delegateTask = new SynchronousDelegateTask(_logger, action, cancellationToken);
            _executionQueue.Add(delegateTask);
            return delegateTask.Task;
        }

        public Task<Collection<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken)
        {
            var psTask = new SynchronousPowerShellTask<TResult>(_logger, _pwsh, psCommand, executionOptions, cancellationToken);
            _executionQueue.Add(psTask);
            return psTask.Task;
        }

        public Task ExecutePSCommandAsync(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken) => ExecutePSCommandAsync<PSObject>(psCommand, executionOptions, cancellationToken);

        public void Stop()
        {
            _stopThreadCancellationSource.Cancel();
            _pipelineThread.Join();
        }

        public void CancelCurrentTask()
        {
            if (_currentExecutionCancellationSource != null)
            {
                _currentExecutionCancellationSource.Cancel();
            }
        }

        public void Dispose()
        {
            Stop();
            _pwsh.Dispose();
        }

        private void Start()
        {
            _pipelineThread = new Thread(RunConsumerLoop)
            {
                Name = "PSES Execution Service Thread"
            };
            _pipelineThread.Start();
        }

        private void RunConsumerLoop()
        {
            Runspace.DefaultRunspace = _pwsh.Runspace;

            try
            {
                foreach (ISynchronousTask synchronousTask in _executionQueue.GetConsumingEnumerable())
                {
                    if (synchronousTask.IsCanceled)
                    {
                        continue;
                    }

                    synchronousTask.ExecuteSynchronously(ref _currentExecutionCancellationSource, _stopThreadCancellationSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // End nicely
            }
        }


        private void EnqueueModuleImport(string moduleNameOrPath)
        {
            var command = new PSCommand()
                .AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                .AddParameter("-Name", moduleNameOrPath);

            ExecutePSCommandAsync(command, new PowerShellExecutionOptions(), CancellationToken.None);
        }
    }
}
