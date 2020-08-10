using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json.Bson;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    internal partial class PowerShellExecutionService : IDisposable
    {

        public static PowerShellExecutionService CreateAndStart(
            ILoggerFactory loggerFactory,
            ILanguageServer languageServer,
            HostStartupInfo hostInfo)
        {
            var executionService = new PowerShellExecutionService(
                loggerFactory,
                languageServer,
                hostInfo.Name,
                hostInfo.Version,
                hostInfo.LanguageMode,
                hostInfo.PSHost,
                hostInfo.ProfilePaths,
                hostInfo.AdditionalModules);

            executionService.Start();

            return executionService;
        }

        private readonly BlockingCollection<ISynchronousTask> _executionQueue;

        private Thread _pipelineThread;

        private bool _runIdleLoop;

        private bool _isExiting;

        private PowerShellExecutionService(
            ILoggerFactory loggerFactory,
            ILanguageServer languageServer,
            string hostName,
            Version hostVersion,
            PSLanguageMode languageMode,
            PSHost internalHost,
            ProfilePathInfo profilePaths,
            IReadOnlyList<string> additionalModules)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<PowerShellExecutionService>();
            _languageServer = languageServer;
            _consoleRepl = new ConsoleReplRunner(_loggerFactory, this);
            _consumerThreadCancellationSource = new CancellationTokenSource();
            _loopCancellationStack = new ConcurrentStack<CancellationTokenSource>();
            _commandCancellationStack = new ConcurrentStack<CancellationTokenSource>();
            _executionQueue = new BlockingCollection<ISynchronousTask>();
            _debuggingContext = new DebuggingContext();
            _psFrameStack = new Stack<PowerShellContextFrame>();
            _taskProcessingLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _hostName = hostName;
            _hostVersion = hostVersion;
            _languageMode = languageMode;
            _internalHost = internalHost;
            _profilePaths = profilePaths;
            _additionalModulesToLoad = additionalModules;
        }

        public EngineIntrinsics EngineIntrinsics { get; private set; }

        public EditorServicesConsolePSHost EditorServicesHost { get; private set; }

        public PSReadLineProxy PSReadLineProxy { get; private set; }

        public ConsoleReadLine ReadLine { get; private set; }

        internal SMA.PowerShell CurrentPowerShell => _psFrameStack.Peek().PowerShell;

        internal CancellationTokenSource CurrentPowerShellCancellationSource => _psFrameStack.Peek().CancellationTokenSource;

        public bool IsCurrentlyRemote => EditorServicesHost.Runspace.RunspaceIsRemote;

        public bool IsDebuggerStopped { get; private set; }

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;

        public event Action<object, DebuggerResumingEventArgs> DebuggerResuming;

        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
        {
            return QueueTask(new SynchronousPSDelegateTask<TResult>(_logger, new PowerShellRunspaceContext(this), func, representation, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            Action<SMA.PowerShell, CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
        {
            return QueueTask(new SynchronousPSDelegateTask(_logger, new PowerShellRunspaceContext(this), action, representation, cancellationToken));
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
                new PowerShellRunspaceContext(this),
                EditorServicesHost,
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

        private Task<T> QueueTask<T>(SynchronousTask<T> task)
        {
            _executionQueue.Add(task);
            return task.Task;
        }

        private void PushNestedPowerShell(PowerShellFrameType frameType)
        {
            SMA.PowerShell pwsh = CreateNestedPowerShell();
            PowerShellFrameType newFrameType = _psFrameStack.Peek().FrameType | PowerShellFrameType.Nested | frameType;
            PushFrame(new PowerShellContextFrame(pwsh, newFrameType, new CancellationTokenSource()));
        }

        private SMA.PowerShell CreateNestedPowerShell()
        {
            PowerShellContextFrame currentFrame = _psFrameStack.Peek();
            if ((currentFrame.FrameType & PowerShellFrameType.Remote) != 0)
            {
                var remotePwsh = SMA.PowerShell.Create();
                remotePwsh.Runspace = currentFrame.PowerShell.Runspace;
                return remotePwsh;
            }

            // PowerShell.CreateNestedPowerShell() sets IsNested but not IsChild
            // This means it throws due to the parent pipeline not running...
            // So we must use the RunspaceMode.CurrentRunspace option on PowerShell.Create() instead
            var pwsh = SMA.PowerShell.Create(RunspaceMode.CurrentRunspace);
            pwsh.Runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            return pwsh;
        }

        private void PushNonInteractivePowerShell()
        {
            PushNestedPowerShell(PowerShellFrameType.NonInteractive);
        }

        private void PushNestedPowerShell()
        {
            PushNestedPowerShell(PowerShellFrameType.Normal);
        }

        private void PushDebugPowerShell()
        {
            PushNestedPowerShell(PowerShellFrameType.Debug);
        }

        private void PushPowerShell(Runspace runspace)
        {
            var pwsh = SMA.PowerShell.Create();
            pwsh.Runspace = runspace;

            PowerShellFrameType frameType = PowerShellFrameType.Normal;

            if (runspace.RunspaceIsRemote)
            {
                frameType |= PowerShellFrameType.Remote;
            }

            PushFrame(new PowerShellContextFrame(pwsh, frameType, new CancellationTokenSource()));
        }

        private void PushFrame(PowerShellContextFrame frame)
        {
            if (_psFrameStack.Count > 0)
            {
                RemoveRunspaceEventHandlers(CurrentPowerShell.Runspace);
            }
            AddRunspaceEventHandlers(frame.PowerShell.Runspace);
            _psFrameStack.Push(frame);
            RunPowerShellLoop(frame.FrameType);
        }

        private void PopFrame()
        {
            _isExiting = false;
            PowerShellContextFrame frame = _psFrameStack.Pop();
            try
            {
                RemoveRunspaceEventHandlers(frame.PowerShell.Runspace);
                if (_psFrameStack.Count > 0)
                {
                    AddRunspaceEventHandlers(CurrentPowerShell.Runspace);
                }
            }
            finally
            {
                frame.Dispose();
            }
        }

        private void RunPowerShellLoop(PowerShellFrameType powerShellFrameType)
        {
            var cancellationContext = LoopCancellationContext.EnterNew(
                this,
                CurrentPowerShellCancellationSource,
                _consumerThreadCancellationSource);

            try
            {
                if (_runIdleLoop)
                {
                    RunIdleLoop(cancellationContext);
                    return;
                }

                _consoleRepl.PushNewReplTask();

                if ((powerShellFrameType & PowerShellFrameType.Debug) != 0)
                {
                    RunDebugLoop(cancellationContext);
                    return;
                }

                RunNestedLoop(cancellationContext);
            }
            finally
            {
                _runIdleLoop = false;
                PopFrame();
                cancellationContext.Dispose();
            }
        }

        internal struct PowerShellRunspaceContext
        {
            private readonly PowerShellExecutionService _executionService;

            public PowerShellRunspaceContext(PowerShellExecutionService executionService)
            {
                _executionService = executionService;
            }

            public Runspace Runspace => _executionService.CurrentPowerShell.Runspace;

            public bool IsRunspacePushed => _executionService._psFrameStack.Count > 1;

            public SMA.PowerShell CurrentPowerShell => _executionService.CurrentPowerShell;

            public void SetShouldExit()
            {
                if (_executionService._psFrameStack.Count <= 1)
                {
                    return;
                }

                _executionService._isExiting = true;

                if ((_executionService._psFrameStack.Peek().FrameType & PowerShellFrameType.NonInteractive) == 0)
                {
                    _executionService._consoleRepl.SetReplPop();
                }
            }

            public void ProcessDebuggerResult(DebuggerCommandResults debuggerResult)
            {
                if (debuggerResult.ResumeAction != null)
                {
                    _executionService.SetDebuggerResuming(debuggerResult.ResumeAction.Value);
                }
            }

            public void PushNestedPowerShell()
            {
                _executionService.PushNestedPowerShell();
            }

            public void PushPowerShell(Runspace runspace)
            {
                _executionService.PushPowerShell(runspace);
            }
        }

        private class DebuggingContext
        {
            private CancellationTokenSource _debuggerCancellationTokenSource;

            public CancellationToken DebuggerResumeCancellationToken => _debuggerCancellationTokenSource.Token;

            public DebuggerStopEventArgs LastStopEventArgs { get; private set; }

            public bool HasStopped => _debuggerCancellationTokenSource != null;

            public void ResetCurrentStopContext()
            {
                LastStopEventArgs = null;
                _debuggerCancellationTokenSource.Dispose();
                _debuggerCancellationTokenSource = null;
            }

            public void OnDebuggerStop(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
            {
                _debuggerCancellationTokenSource = new CancellationTokenSource();
                LastStopEventArgs = debuggerStopEventArgs;
            }

            public void SetDebuggerResuming(DebuggerResumeAction resumeAction)
            {
                LastStopEventArgs.ResumeAction = resumeAction;
                _debuggerCancellationTokenSource.Cancel();
            }
        }

        private readonly struct LoopCancellationContext : IDisposable
        {
            public static LoopCancellationContext EnterNew(
                PowerShellExecutionService executionService,
                CancellationTokenSource cts1,
                CancellationTokenSource cts2)
            {
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cts2.Token);
                executionService._loopCancellationStack.Push(cancellationTokenSource);
                return new LoopCancellationContext(executionService._loopCancellationStack, cancellationTokenSource.Token);
            }

            private readonly ConcurrentStack<CancellationTokenSource> _loopCancellationStack;

            public readonly CancellationToken CancellationToken;

            private LoopCancellationContext(
                ConcurrentStack<CancellationTokenSource> loopCancellationStack,
                CancellationToken cancellationToken)
            {
                _loopCancellationStack = loopCancellationStack;
                CancellationToken = cancellationToken;
            }

            public void Dispose()
            {
                if (_loopCancellationStack.TryPop(out CancellationTokenSource loopCancellation))
                {
                    loopCancellation.Dispose();
                }
            }
        }

        private readonly struct TaskCancellationContext : IDisposable
        {
            public static TaskCancellationContext EnterNew(PowerShellExecutionService executionService, CancellationToken loopCancellationToken)
            {
                executionService._taskProcessingLock.EnterReadLock();
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(loopCancellationToken);
                executionService._commandCancellationStack.Push(cancellationTokenSource);
                return new TaskCancellationContext(executionService, cancellationTokenSource.Token);
            }

            private TaskCancellationContext(PowerShellExecutionService executionService, CancellationToken cancellationToken)
            {
                _executionService = executionService;
                CancellationToken = cancellationToken;
            }

            private readonly PowerShellExecutionService _executionService;

            public readonly CancellationToken CancellationToken;

            public void Dispose()
            {
                _executionService._taskProcessingLock.ExitReadLock();
                if (_executionService._commandCancellationStack.TryPop(out CancellationTokenSource taskCancellation))
                {
                    taskCancellation.Dispose();
                }
            }
        }
    }
}
