using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System;
using System.Management.Automation;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{

    internal class PipelineThreadExecutor
    {
        private static readonly PropertyInfo s_shouldProcessInExecutionThreadProperty =
            typeof(PSEventSubscriber)
                .GetProperty(
                    "ShouldProcessInExecutionThread",
                    BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        private readonly EditorServicesConsolePSHost _psesHost;

        private readonly IReadLineProvider _readLineProvider;

        private readonly HostStartupInfo _hostInfo;

        private readonly BlockingConcurrentDeque<ISynchronousTask> _taskQueue;

        private readonly CancellationTokenSource _consumerThreadCancellationSource;

        private readonly Thread _pipelineThread; 

        private readonly CancellationContext _loopCancellationContext;

        private readonly CancellationContext _commandCancellationContext;

        private readonly ManualResetEventSlim _taskProcessingAllowed;

        private bool _runIdleLoop;

        public PipelineThreadExecutor(
            ILoggerFactory loggerFactory,
            HostStartupInfo hostInfo,
            EditorServicesConsolePSHost psesHost,
            IReadLineProvider readLineProvider)
        {
            _logger = loggerFactory.CreateLogger<PipelineThreadExecutor>();
            _hostInfo = hostInfo;
            _psesHost = psesHost;
            _readLineProvider = readLineProvider;
            _consumerThreadCancellationSource = new CancellationTokenSource();
            _taskQueue = new BlockingConcurrentDeque<ISynchronousTask>();
            _loopCancellationContext = new CancellationContext();
            _commandCancellationContext = new CancellationContext();
            _taskProcessingAllowed = new ManualResetEventSlim(initialState: true);

            _pipelineThread = new Thread(Run)
            {
                Name = "PSES Execution Service Thread",
            };
            _pipelineThread.SetApartmentState(ApartmentState.STA);
        }

        public bool IsExiting { get; set; }

        public Task<TResult> RunTaskAsync<TResult>(SynchronousTask<TResult> synchronousTask)
        {
            if (synchronousTask.ExecutionOptions.InterruptCurrentForeground)
            {
                return CancelCurrentAndRunTaskNowAsync(synchronousTask);
            }

            switch (synchronousTask.ExecutionOptions.Priority)
            {
                case ExecutionPriority.Next:
                    _taskQueue.Prepend(synchronousTask);
                    break;

                case ExecutionPriority.Normal:
                    _taskQueue.Append(synchronousTask);
                    break;
            }

            return synchronousTask.Task;
        }

        public void Start()
        {
            _pipelineThread.Start();
        }

        public void Stop()
        {
            _consumerThreadCancellationSource.Cancel();
            _pipelineThread.Join();
        }

        public void CancelCurrentTask()
        {
            _commandCancellationContext.CancelCurrentTask();
        }

        public void Dispose()
        {
            Stop();
        }

        private Task<TResult> CancelCurrentAndRunTaskNowAsync<TResult>(SynchronousTask<TResult> synchronousTask)
        {
            // We need to ensure that we don't:
            // - Add this command to the queue and immediately cancel it
            // - Allow a consumer to dequeue and run another command after cancellation and before we add this command
            //
            // To ensure that, we need the following sequence:
            // - Stop queue consumption progressing
            // - Cancel any current processing
            // - Add our task to the front of the queue
            // - Recommence processing

            using (_taskQueue.BlockConsumers())
            {
                _commandCancellationContext.CancelCurrentTaskStack();
                _taskQueue.Prepend(synchronousTask);
                return synchronousTask.Task;
            }
        }

        private void Run()
        {
            _psesHost.PushInitialPowerShell();
            // We need to override the idle handler here,
            // since readline will be overridden when the initial Powershell runspace is instantiated above
            _readLineProvider.ReadLine.TryOverrideIdleHandler(OnPowerShellIdle);
            _psesHost.StartRepl();
            RunTopLevelConsumerLoop();
        }

        public void RunPowerShellLoop(PowerShellFrameType powerShellFrameType)
        {
            using (CancellationScope cancellationScope = _loopCancellationContext.EnterScope(_psesHost.CurrentCancellationSource.Token, _consumerThreadCancellationSource.Token))
            {
                try
                {
                    if (_runIdleLoop)
                    {
                        RunIdleLoop(cancellationScope);
                        return;
                    }

                    _psesHost.PushNewReplTask();

                    if ((powerShellFrameType & PowerShellFrameType.Debug) != 0)
                    {
                        RunDebugLoop(cancellationScope);
                        return;
                    }

                    RunNestedLoop(cancellationScope);
                }
                finally
                {
                    _runIdleLoop = false;
                    _psesHost.PopPowerShell();
                }
            }
        }

        private void RunTopLevelConsumerLoop()
        {
            using (CancellationScope cancellationScope = _loopCancellationContext.EnterScope(_psesHost.CurrentCancellationSource.Token, _consumerThreadCancellationSource.Token))
            {
                try
                {
                    while (true)
                    {
                        RunNextForegroundTaskSynchronously(cancellationScope.CancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Catch cancellations to end nicely
                }
            }
        }

        private void RunNestedLoop(in CancellationScope cancellationScope)
        {
            try
            {
                while (true)
                {
                    RunNextForegroundTaskSynchronously(cancellationScope.CancellationToken);

                    if (IsExiting)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Catch cancellations to end nicely
            }
        }

        private void RunDebugLoop(in CancellationScope cancellationScope)
        {
            _psesHost.DebugContext.EnterDebugLoop(cancellationScope.CancellationToken);
            try
            {
                // Run commands, but cancelling our blocking wait if the debugger resumes
                while (true)
                {
                    ISynchronousTask task = _taskQueue.Take(_psesHost.DebugContext.OnResumeCancellationToken);

                    // We don't want to cancel the current command when the debugger resumes,
                    // since that command will be resuming the debugger.
                    // Instead let it complete and check the cancellation afterward.
                    RunTaskSynchronously(task, cancellationScope.CancellationToken);

                    if (_psesHost.DebugContext.OnResumeCancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Catch cancellations to end nicely
            }
            finally
            {
                _psesHost.DebugContext.ExitDebugLoop();
            }
        }

        private void RunIdleLoop(in CancellationScope cancellationScope)
        {
            try
            {
                while (!cancellationScope.CancellationToken.IsCancellationRequested
                        && _taskQueue.TryTake(out ISynchronousTask task))
                {
                    if (task.ExecutionOptions.MustRunInForeground)
                    {
                        _taskQueue.Prepend(task);
                        break;
                    }

                    RunTaskSynchronously(task, cancellationScope.CancellationToken);
                }

                // TODO: Handle engine events here using a nested pipeline
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void RunNextForegroundTaskSynchronously(CancellationToken loopCancellationToken)
        {
            ISynchronousTask task = _taskQueue.Take(loopCancellationToken);
            RunTaskSynchronously(task, loopCancellationToken);
        }

        private void RunTaskSynchronously(ISynchronousTask task, CancellationToken loopCancellationToken)
        {
            if (task.IsCanceled)
            {
                return;
            }

            using (CancellationScope commandCancellationScope = _commandCancellationContext.EnterScope(loopCancellationToken))
            {
                task.ExecuteSynchronously(commandCancellationScope.CancellationToken);
            }
        }

        public void OnPowerShellIdle()
        {
            if (_taskQueue.Count == 0)
            {
                return;
            }

            _runIdleLoop = true;
            _psesHost.PushNonInteractivePowerShell();
        }
    }
}
