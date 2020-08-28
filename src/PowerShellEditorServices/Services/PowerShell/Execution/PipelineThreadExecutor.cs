using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using System;
using System.Collections.Concurrent;
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

        private readonly PowerShellDebugContext _debugContext;

        private readonly IReadLineProvider _readLineProvider;

        private readonly HostStartupInfo _hostInfo;

        private readonly BlockingCollection<ISynchronousTask> _executionQueue;

        private readonly CancellationTokenSource _consumerThreadCancellationSource;

        private readonly Thread _pipelineThread; 

        private readonly CancellationContext _loopCancellationContext;

        private readonly CancellationContext _commandCancellationContext;

        private readonly ReaderWriterLockSlim _taskProcessingLock;

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
            _debugContext = psesHost.DebugContext;
            _readLineProvider = readLineProvider;

            _pipelineThread = new Thread(Run)
            {
                Name = "PSES Execution Service Thread",
            };
            _pipelineThread.SetApartmentState(ApartmentState.STA);
        }

        public bool IsExiting { get; set; }

        public Task<TResult> QueueTask<TResult>(SynchronousTask<TResult> synchronousTask)
        {
            _executionQueue.Add(synchronousTask);
            return synchronousTask.Task;
        }
        public void Start()
        {
            // We need to override the idle handler here,
            // since readline will be overridden by this point
            _readLineProvider.ReadLine.TryOverrideIdleHandler(OnPowerShellIdle);
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

        public IDisposable TakeTaskWriterLock()
        {
            return TaskProcessingWriterLockLifetime.TakeLock(_taskProcessingLock);
        }

        private void Run()
        {
            _psesHost.PushInitialPowerShell();
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
                    foreach (ISynchronousTask task in _executionQueue.GetConsumingEnumerable(cancellationScope.CancellationToken))
                    {
                        RunTaskSynchronously(task, cancellationScope.CancellationToken);
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
                foreach (ISynchronousTask task in _executionQueue.GetConsumingEnumerable(cancellationScope.CancellationToken))
                {
                    RunTaskSynchronously(task, cancellationScope.CancellationToken);

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
            _debugContext.EnterDebugLoop(cancellationScope.CancellationToken);
            try
            {
                // Run commands, but cancelling our blocking wait if the debugger resumes
                foreach (ISynchronousTask task in _executionQueue.GetConsumingEnumerable(_debugContext.OnResumeCancellationToken))
                {
                    // We don't want to cancel the current command when the debugger resumes,
                    // since that command will be resuming the debugger.
                    // Instead let it complete and check the cancellation afterward.
                    RunTaskSynchronously(task, cancellationScope.CancellationToken);

                    if (_debugContext.OnResumeCancellationToken.IsCancellationRequested)
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
                _debugContext.ExitDebugLoop();
            }
        }

        private void RunIdleLoop(in CancellationScope cancellationScope)
        {
            try
            {
                while (_executionQueue.TryTake(out ISynchronousTask task))
                {
                    RunTaskSynchronously(task, cancellationScope.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {

            }

            // TODO: Run nested pipeline here for engine event handling
        }

        private void RunTaskSynchronously(ISynchronousTask task, CancellationToken loopCancellationToken)
        {
            if (task.IsCanceled)
            {
                return;
            }

            using (CancellationScope commandCancellationScope = _commandCancellationContext.EnterScope(loopCancellationToken))
            {
                _taskProcessingLock.EnterReadLock();
                try
                {
                    task.ExecuteSynchronously(commandCancellationScope.CancellationToken);
                }
                finally
                {
                    _taskProcessingLock.ExitReadLock();
                }
            }
        }

        public void OnPowerShellIdle()
        {
            if (_executionQueue.Count == 0)
            {
                return;
            }

            _runIdleLoop = true;
            _psesHost.PushNonInteractivePowerShell();
        }

        private struct TaskProcessingWriterLockLifetime : IDisposable
        {
            private readonly ReaderWriterLockSlim _rwLock;

            public static TaskProcessingWriterLockLifetime TakeLock(ReaderWriterLockSlim rwLock)
            {
                rwLock.EnterWriteLock();
                return new TaskProcessingWriterLockLifetime(rwLock);
            }

            private TaskProcessingWriterLockLifetime(ReaderWriterLockSlim rwLock)
            {
                _rwLock = rwLock;
            }

            public void Dispose()
            {
                _rwLock.ExitWriteLock();
            }
        }
    }
}
