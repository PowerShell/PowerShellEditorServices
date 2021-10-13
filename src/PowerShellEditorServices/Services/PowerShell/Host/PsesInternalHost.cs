using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation.Host;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
    using Microsoft.PowerShell.EditorServices.Utility;
    using System.IO;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PsesInternalHost : PSHost, IHostSupportsInteractiveSession, IRunspaceContext, IInternalPowerShellExecutionService
    {
        private const string DefaultPrompt = "PSIC> ";

        private static readonly string s_commandsModulePath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "../../Commands/PowerShellEditorServices.Commands.psd1"));

        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        private readonly ILanguageServerFacade _languageServer;

        private readonly HostStartupInfo _hostInfo;

        private readonly BlockingConcurrentDeque<ISynchronousTask> _taskQueue;

        private readonly Stack<PowerShellContextFrame> _psFrameStack;

        private readonly Stack<RunspaceFrame> _runspaceStack;

        private readonly CancellationContext _cancellationContext;

        private readonly ReadLineProvider _readLineProvider;

        private readonly Thread _pipelineThread;

        private readonly IdempotentLatch _isRunningLatch = new();

        private EngineIntrinsics _mainRunspaceEngineIntrinsics;

        private bool _shouldExit = false;

        private string _localComputerName;

        private ConsoleKeyInfo? _lastKey;

        private bool _skipNextPrompt = false;

        private bool _resettingRunspace = false;

        public PsesInternalHost(
            ILoggerFactory loggerFactory,
            ILanguageServerFacade languageServer,
            HostStartupInfo hostInfo)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<PsesInternalHost>();
            _languageServer = languageServer;
            _hostInfo = hostInfo;

            _readLineProvider = new ReadLineProvider(loggerFactory);
            _taskQueue = new BlockingConcurrentDeque<ISynchronousTask>();
            _psFrameStack = new Stack<PowerShellContextFrame>();
            _runspaceStack = new Stack<RunspaceFrame>();
            _cancellationContext = new CancellationContext();

            _pipelineThread = new Thread(Run)
            {
                Name = "PSES Pipeline Execution Thread",
            };

            _pipelineThread.SetApartmentState(ApartmentState.STA);

            PublicHost = new EditorServicesConsolePSHost(this);
            Name = hostInfo.Name;
            Version = hostInfo.Version;

            DebugContext = new PowerShellDebugContext(loggerFactory, languageServer, this);
            UI = new EditorServicesConsolePSHostUserInterface(loggerFactory, _readLineProvider, hostInfo.PSHost.UI);
        }

        public override CultureInfo CurrentCulture => _hostInfo.PSHost.CurrentCulture;

        public override CultureInfo CurrentUICulture => _hostInfo.PSHost.CurrentUICulture;

        public override Guid InstanceId { get; } = Guid.NewGuid();

        public override string Name { get; }

        public override PSHostUserInterface UI { get; }

        public override Version Version { get; }

        public bool IsRunspacePushed { get; private set; }

        public Runspace Runspace => _runspaceStack.Peek().Runspace;

        public RunspaceInfo CurrentRunspace => CurrentFrame.RunspaceInfo;

        public SMA.PowerShell CurrentPowerShell => CurrentFrame.PowerShell;

        public EditorServicesConsolePSHost PublicHost { get; }

        public PowerShellDebugContext DebugContext { get; }

        public bool IsRunning => _isRunningLatch.IsSignaled;

        public string InitialWorkingDirectory { get; private set; }

        IRunspaceInfo IRunspaceContext.CurrentRunspace => CurrentRunspace;

        private PowerShellContextFrame CurrentFrame => _psFrameStack.Peek();

        public event Action<object, RunspaceChangedEventArgs> RunspaceChanged;

        public override void EnterNestedPrompt()
        {
            PushPowerShellAndRunLoop(CreateNestedPowerShell(CurrentRunspace), PowerShellFrameType.Nested);
        }

        public override void ExitNestedPrompt()
        {
            SetExit();
        }

        public override void NotifyBeginApplication()
        {
            // TODO: Work out what to do here
        }

        public override void NotifyEndApplication()
        {
            // TODO: Work out what to do here
        }

        public void PopRunspace()
        {
            IsRunspacePushed = false;
            SetExit();
        }

        public void PushRunspace(Runspace runspace)
        {
            IsRunspacePushed = true;
            PushPowerShellAndRunLoop(CreatePowerShellForRunspace(runspace), PowerShellFrameType.Remote);
        }

        public override void SetShouldExit(int exitCode)
        {
            // TODO: Handle exit code if needed
            SetExit();
        }

        public async Task StartAsync(HostStartOptions startOptions, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Host starting");
            if (!_isRunningLatch.TryEnter())
            {
                _logger.LogDebug("Host start requested after already started");
                return;
            }

            _pipelineThread.Start();

            if (startOptions.LoadProfiles)
            {
                await ExecuteDelegateAsync(
                    "LoadProfiles",
                    new PowerShellExecutionOptions { MustRunInForeground = true, ThrowOnError = false },
                    (pwsh, delegateCancellation) => pwsh.LoadProfiles(_hostInfo.ProfilePaths),
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Profiles loaded");
            }

            if (startOptions.InitialWorkingDirectory is not null)
            {
                await SetInitialWorkingDirectoryAsync(startOptions.InitialWorkingDirectory, CancellationToken.None).ConfigureAwait(false);
            }
        }

        public void SetExit()
        {
            // Can't exit from the top level of PSES
            // since if you do, you lose all LSP services
            if (_psFrameStack.Count <= 1)
            {
                return;
            }

            _shouldExit = true;
        }

        public Task<T> InvokeTaskOnPipelineThreadAsync<T>(
            SynchronousTask<T> task)
        {
            if (task.ExecutionOptions.InterruptCurrentForeground)
            {
                // When a task must displace the current foreground command,
                // we must:
                //  - block the consumer thread from mutating the queue
                //  - cancel any running task on the consumer thread
                //  - place our task on the front of the queue
                //  - unblock the consumer thread
                using (_taskQueue.BlockConsumers())
                {
                    CancelCurrentTask();
                    _taskQueue.Prepend(task);
                }

                return task.Task;
            }

            switch (task.ExecutionOptions.Priority)
            {
                case ExecutionPriority.Next:
                    _taskQueue.Prepend(task);
                    break;

                case ExecutionPriority.Normal:
                    _taskQueue.Append(task);
                    break;
            }

            return task.Task;
        }

        public void CancelCurrentTask()
        {
            _cancellationContext.CancelCurrentTask();
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            CancellationToken cancellationToken)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousPSDelegateTask<TResult>(_logger, this, representation, executionOptions ?? ExecutionOptions.Default, func, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            Action<SMA.PowerShell, CancellationToken> action,
            CancellationToken cancellationToken)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousPSDelegateTask(_logger, this, representation, executionOptions ?? ExecutionOptions.Default, action, cancellationToken));
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            Func<CancellationToken, TResult> func,
            CancellationToken cancellationToken)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousDelegateTask<TResult>(_logger, representation, executionOptions ?? ExecutionOptions.Default, func, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            Action<CancellationToken> action,
            CancellationToken cancellationToken)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousDelegateTask(_logger, representation, executionOptions ?? ExecutionOptions.Default, action, cancellationToken));
        }

        public Task<IReadOnlyList<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousPowerShellTask<TResult>(
                _logger,
                this,
                psCommand,
                executionOptions ?? PowerShellExecutionOptions.Default,
                cancellationToken));
        }

        public Task ExecutePSCommandAsync(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null) => ExecutePSCommandAsync<PSObject>(psCommand, cancellationToken, executionOptions);

        public TResult InvokeDelegate<TResult>(string representation, ExecutionOptions executionOptions, Func<CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            var task = new SynchronousDelegateTask<TResult>(_logger, representation, executionOptions, func, cancellationToken);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokeDelegate(string representation, ExecutionOptions executionOptions, Action<CancellationToken> action, CancellationToken cancellationToken)
        {
            var task = new SynchronousDelegateTask(_logger, representation, executionOptions, action, cancellationToken);
            task.ExecuteAndGetResult(cancellationToken);
        }

        public IReadOnlyList<TResult> InvokePSCommand<TResult>(PSCommand psCommand, PowerShellExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            var task = new SynchronousPowerShellTask<TResult>(_logger, this, psCommand, executionOptions, cancellationToken);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokePSCommand(PSCommand psCommand, PowerShellExecutionOptions executionOptions, CancellationToken cancellationToken)
            => InvokePSCommand<PSObject>(psCommand, executionOptions, cancellationToken);

        public TResult InvokePSDelegate<TResult>(string representation, ExecutionOptions executionOptions, Func<SMA.PowerShell, CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            var task = new SynchronousPSDelegateTask<TResult>(_logger, this, representation, executionOptions, func, cancellationToken);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokePSDelegate(string representation, ExecutionOptions executionOptions, Action<SMA.PowerShell, CancellationToken> action, CancellationToken cancellationToken)
        {
            var task = new SynchronousPSDelegateTask(_logger, this, representation, executionOptions, action, cancellationToken);
            task.ExecuteAndGetResult(cancellationToken);
        }

        public Task SetInitialWorkingDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            InitialWorkingDirectory = path;

            return ExecutePSCommandAsync(
                new PSCommand().AddCommand("Set-Location").AddParameter("LiteralPath", path),
                cancellationToken);
        }

        private void Run()
        {
            (PowerShell pwsh, RunspaceInfo localRunspaceInfo, EngineIntrinsics engineIntrinsics) = CreateInitialPowerShellSession();
            _mainRunspaceEngineIntrinsics = engineIntrinsics;
            _localComputerName = localRunspaceInfo.SessionDetails.ComputerName;
            _runspaceStack.Push(new RunspaceFrame(pwsh.Runspace, localRunspaceInfo));
            PushPowerShellAndRunLoop(pwsh, PowerShellFrameType.Normal, localRunspaceInfo);
        }

        private (PowerShell, RunspaceInfo, EngineIntrinsics) CreateInitialPowerShellSession()
        {
            (PowerShell pwsh, EngineIntrinsics engineIntrinsics) = CreateInitialPowerShell(_hostInfo, _readLineProvider);
            RunspaceInfo localRunspaceInfo = RunspaceInfo.CreateFromLocalPowerShell(_logger, pwsh);
            return (pwsh, localRunspaceInfo, engineIntrinsics);
        }

        private void PushPowerShellAndRunLoop(SMA.PowerShell pwsh, PowerShellFrameType frameType, RunspaceInfo newRunspaceInfo = null)
        {
            // TODO: Improve runspace origin detection here
            if (newRunspaceInfo is null)
            {
                newRunspaceInfo = GetRunspaceInfoForPowerShell(pwsh, out bool isNewRunspace, out RunspaceFrame oldRunspaceFrame);

                if (isNewRunspace)
                {
                    Runspace newRunspace = pwsh.Runspace;
                    _runspaceStack.Push(new RunspaceFrame(newRunspace, newRunspaceInfo));
                    RunspaceChanged.Invoke(this, new RunspaceChangedEventArgs(RunspaceChangeAction.Enter, oldRunspaceFrame.RunspaceInfo, newRunspaceInfo));
                }
            }

            PushPowerShellAndRunLoop(new PowerShellContextFrame(pwsh, newRunspaceInfo, frameType));
        }

        private RunspaceInfo GetRunspaceInfoForPowerShell(SMA.PowerShell pwsh, out bool isNewRunspace, out RunspaceFrame oldRunspaceFrame)
        {
            oldRunspaceFrame = null;

            if (_runspaceStack.Count > 0)
            {
                // This is more than just an optimization.
                // When debugging, we cannot execute PowerShell directly to get this information;
                // trying to do so will block on the command that called us, deadlocking execution.
                // Instead, since we are reusing the runspace, we reuse that runspace's info as well.
                oldRunspaceFrame = _runspaceStack.Peek();
                if (oldRunspaceFrame.Runspace == pwsh.Runspace)
                {
                    isNewRunspace = false;
                    return oldRunspaceFrame.RunspaceInfo;
                }
            }

            isNewRunspace = true;
            return RunspaceInfo.CreateFromPowerShell(_logger, pwsh, _localComputerName);
        }

        private void PushPowerShellAndRunLoop(PowerShellContextFrame frame)
        {
            PushPowerShell(frame);

            try
            {
                if (_psFrameStack.Count == 1)
                {
                    RunTopLevelExecutionLoop();
                }
                else if ((frame.FrameType & PowerShellFrameType.Debug) != 0)
                {
                    RunDebugExecutionLoop();
                }
                else
                {
                    RunExecutionLoop();
                }
            }
            finally
            {
                PopPowerShell();
            }
        }

        private void PushPowerShell(PowerShellContextFrame frame)
        {
            if (_psFrameStack.Count > 0)
            {
                RemoveRunspaceEventHandlers(CurrentFrame.PowerShell.Runspace);
            }

            AddRunspaceEventHandlers(frame.PowerShell.Runspace);

            _psFrameStack.Push(frame);
        }

        private void PopPowerShell(RunspaceChangeAction runspaceChangeAction = RunspaceChangeAction.Exit)
        {
            _shouldExit = false;
            PowerShellContextFrame frame = _psFrameStack.Pop();
            try
            {
                // If we're changing runspace, make sure we move the handlers over
                RunspaceFrame previousRunspaceFrame = _runspaceStack.Peek();
                if (previousRunspaceFrame.Runspace != CurrentPowerShell.Runspace)
                {
                    _runspaceStack.Pop();
                    RunspaceFrame currentRunspaceFrame = _runspaceStack.Peek();
                    RemoveRunspaceEventHandlers(previousRunspaceFrame.Runspace);
                    AddRunspaceEventHandlers(currentRunspaceFrame.Runspace);
                    RunspaceChanged?.Invoke(this, new RunspaceChangedEventArgs(runspaceChangeAction, previousRunspaceFrame.RunspaceInfo, currentRunspaceFrame.RunspaceInfo));
                }
            }
            finally
            {
                frame.Dispose();
            }
        }

        private void RunTopLevelExecutionLoop()
        {
            // Make sure we execute any startup tasks first
            while (_taskQueue.TryTake(out ISynchronousTask task))
            {
                task.ExecuteSynchronously(CancellationToken.None);
            }

            RunExecutionLoop();
        }

        private void RunDebugExecutionLoop()
        {
            try
            {
                DebugContext.EnterDebugLoop(CancellationToken.None);
                RunExecutionLoop();
            }
            finally
            {
                DebugContext.ExitDebugLoop();
            }
        }

        private void RunExecutionLoop()
        {
            while (!_shouldExit)
            {
                using (CancellationScope cancellationScope = _cancellationContext.EnterScope(isIdleScope: false))
                {
                    DoOneRepl(cancellationScope.CancellationToken);

                    while (!_shouldExit
                        && !cancellationScope.CancellationToken.IsCancellationRequested
                        && _taskQueue.TryTake(out ISynchronousTask task))
                    {
                        task.ExecuteSynchronously(cancellationScope.CancellationToken);
                    }
                }
            }
        }

        private void DoOneRepl(CancellationToken cancellationToken)
        {
            // When a task must run in the foreground, we cancel out of the idle loop and return to the top level.
            // At that point, we would normally run a REPL, but we need to immediately execute the task.
            // So we set _skipNextPrompt to do that.
            if (_skipNextPrompt)
            {
                _skipNextPrompt = false;
                return;
            }

            try
            {
                string prompt = GetPrompt(cancellationToken);
                UI.Write(prompt);
                string userInput = InvokeReadLine(cancellationToken);

                // If the user input was empty it's because:
                //  - the user provided no input
                //  - the readline task was canceled
                //  - CtrlC was sent to readline (which does not propagate a cancellation)
                //
                // In any event there's nothing to run in PowerShell, so we just loop back to the prompt again.
                // However, we must distinguish the last two scenarios, since PSRL will not print a new line in those cases.
                if (string.IsNullOrEmpty(userInput))
                {
                    if (cancellationToken.IsCancellationRequested
                        || LastKeyWasCtrlC())
                    {
                        UI.WriteLine();
                    }
                    return;
                }

                InvokeInput(userInput, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Do nothing, since we were just cancelled
            }
            catch (Exception e)
            {
                UI.WriteErrorLine($"An error occurred while running the REPL loop:{Environment.NewLine}{e}");
                _logger.LogError(e, "An error occurred while running the REPL loop");
            }
        }

        private string GetPrompt(CancellationToken cancellationToken)
        {
            var command = new PSCommand().AddCommand("prompt");
            IReadOnlyList<string> results = InvokePSCommand<string>(command, PowerShellExecutionOptions.Default, cancellationToken);
            string prompt = results.Count > 0 ? results[0] : DefaultPrompt;

            if (CurrentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                // This is a PowerShell-internal method that we reuse to decorate the prompt string
                // with the remote details when remoting,
                // so the prompt changes to indicate when you're in a remote session
                prompt = Runspace.GetRemotePrompt(prompt);
            }

            return prompt;
        }

        private string InvokeReadLine(CancellationToken cancellationToken)
        {
            return _readLineProvider.ReadLine.ReadLine(cancellationToken);
        }

        private void InvokeInput(string input, CancellationToken cancellationToken)
        {
            var command = new PSCommand().AddScript(input, useLocalScope: false);
            InvokePSCommand(command, new PowerShellExecutionOptions { AddToHistory = true, ThrowOnError = false, WriteOutputToHost = true }, cancellationToken);
        }

        private void AddRunspaceEventHandlers(Runspace runspace)
        {
            runspace.Debugger.DebuggerStop += OnDebuggerStopped;
            runspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
            runspace.StateChanged += OnRunspaceStateChanged;
        }

        private void RemoveRunspaceEventHandlers(Runspace runspace)
        {
            runspace.Debugger.DebuggerStop -= OnDebuggerStopped;
            runspace.Debugger.BreakpointUpdated -= OnBreakpointUpdated;
            runspace.StateChanged -= OnRunspaceStateChanged;
        }

        private static PowerShell CreateNestedPowerShell(RunspaceInfo currentRunspace)
        {
            if (currentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                return CreatePowerShellForRunspace(currentRunspace.Runspace);
            }

            // PowerShell.CreateNestedPowerShell() sets IsNested but not IsChild
            // This means it throws due to the parent pipeline not running...
            // So we must use the RunspaceMode.CurrentRunspace option on PowerShell.Create() instead
            var pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace);
            pwsh.Runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            return pwsh;
        }

        private static PowerShell CreatePowerShellForRunspace(Runspace runspace)
        {
            var pwsh = PowerShell.Create();
            pwsh.Runspace = runspace;
            return pwsh;
        }

        public (PowerShell, EngineIntrinsics) CreateInitialPowerShell(
            HostStartupInfo hostStartupInfo,
            ReadLineProvider readLineProvider)
        {
            Runspace runspace = CreateInitialRunspace(hostStartupInfo.InitialSessionState);
            PowerShell pwsh = CreatePowerShellForRunspace(runspace);

            var engineIntrinsics = (EngineIntrinsics)runspace.SessionStateProxy.GetVariable("ExecutionContext");

            if (hostStartupInfo.ConsoleReplEnabled && !hostStartupInfo.UsesLegacyReadLine)
            {
                var psrlProxy = PSReadLineProxy.LoadAndCreate(_loggerFactory, pwsh);
                var readLine = new ConsoleReadLine(psrlProxy, this, engineIntrinsics);
                readLine.TryOverrideReadKey(ReadKey);
                readLine.TryOverrideIdleHandler(OnPowerShellIdle);
                readLineProvider.OverrideReadLine(readLine);
                System.Console.CancelKeyPress += OnCancelKeyPress;
                System.Console.InputEncoding = Encoding.UTF8;
                System.Console.OutputEncoding = Encoding.UTF8;
            }

            if (VersionUtils.IsWindows)
            {
                pwsh.SetCorrectExecutionPolicy(_logger);
            }

            pwsh.ImportModule(s_commandsModulePath);

            if (hostStartupInfo.AdditionalModules != null && hostStartupInfo.AdditionalModules.Count > 0)
            {
                foreach (string module in hostStartupInfo.AdditionalModules)
                {
                    pwsh.ImportModule(module);
                }
            }

            return (pwsh, engineIntrinsics);
        }

        private Runspace CreateInitialRunspace(InitialSessionState initialSessionState)
        {
            Runspace runspace = RunspaceFactory.CreateRunspace(PublicHost, initialSessionState);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;

            runspace.Open();

            Runspace.DefaultRunspace = runspace;

            return runspace;
        }

        private void OnPowerShellIdle()
        {
            IReadOnlyList<PSEventSubscriber> eventSubscribers = _mainRunspaceEngineIntrinsics.Events.Subscribers;

            // Go through pending event subscribers and:
            // - if we have any subscribers, ensure we process any events
            // - if we have any idle events, generate an idle event and process that
            bool runPipelineForEventProcessing = false;
            foreach (PSEventSubscriber subscriber in eventSubscribers)
            {
                runPipelineForEventProcessing = true;

                if (string.Equals(subscriber.SourceIdentifier, PSEngineEvent.OnIdle, StringComparison.OrdinalIgnoreCase))
                {
                    // We control the pipeline thread, so it's not possible for PowerShell to generate events while we're here.
                    // But we know we're sitting waiting for the prompt, so we generate the idle event ourselves
                    // and that will flush idle event subscribers in PowerShell so we can service them
                    _mainRunspaceEngineIntrinsics.Events.GenerateEvent(PSEngineEvent.OnIdle, sender: null, args: null, extraData: null);
                    break;
                }
            }

            if (!runPipelineForEventProcessing && _taskQueue.Count == 0)
            {
                return;
            }

            using (CancellationScope cancellationScope = _cancellationContext.EnterScope(isIdleScope: true))
            {
                while (!cancellationScope.CancellationToken.IsCancellationRequested
                    && _taskQueue.TryTake(out ISynchronousTask task))
                {
                    if (task.ExecutionOptions.MustRunInForeground)
                    {
                        // If we have a task that is queued, but cannot be run under readline
                        // we place it back at the front of the queue, and cancel the readline task
                        _taskQueue.Prepend(task);
                        _skipNextPrompt = true;
                        _cancellationContext.CancelIdleParentTask();
                        return;
                    }

                    // If we're executing a task, we don't need to run an extra pipeline later for events
                    // TODO: This may not be a PowerShell task, so ideally we can differentiate that here.
                    //       For now it's mostly true and an easy assumption to make.
                    runPipelineForEventProcessing = false;
                    task.ExecuteSynchronously(cancellationScope.CancellationToken);
                }
            }

            // We didn't end up executinng anything in the background,
            // so we need to run a small artificial pipeline instead
            // to force event processing
            if (runPipelineForEventProcessing)
            {
                InvokePSCommand(new PSCommand().AddScript("0", useLocalScope: true), PowerShellExecutionOptions.Default, CancellationToken.None);
            }
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            _cancellationContext.CancelCurrentTask();
        }

        private ConsoleKeyInfo ReadKey(bool intercept)
        {
            // PSRL doesn't tell us when CtrlC was sent.
            // So instead we keep track of the last key here.
            // This isn't functionally required,
            // but helps us determine when the prompt needs a newline added

            _lastKey = ConsoleProxy.SafeReadKey(intercept, CancellationToken.None);
            return _lastKey.Value;
        }

        private bool LastKeyWasCtrlC()
        {
            return _lastKey.HasValue
                && _lastKey.Value.Key == ConsoleKey.C
                && (_lastKey.Value.Modifiers & ConsoleModifiers.Control) != 0
                && (_lastKey.Value.Modifiers & ConsoleModifiers.Alt) == 0;
        }

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
        {
            DebugContext.SetDebuggerStopped(debuggerStopEventArgs);
            try
            {
                CurrentPowerShell.WaitForRemoteOutputIfNeeded();
                PushPowerShellAndRunLoop(CreateNestedPowerShell(CurrentRunspace), PowerShellFrameType.Debug | PowerShellFrameType.Nested);
                CurrentPowerShell.ResumeRemoteOutputIfNeeded();
            }
            finally
            {
                DebugContext.SetDebuggerResumed();
            }
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs breakpointUpdatedEventArgs)
        {
            DebugContext.HandleBreakpointUpdated(breakpointUpdatedEventArgs);
        }

        private void OnRunspaceStateChanged(object sender, RunspaceStateEventArgs runspaceStateEventArgs)
        {
            if (!_shouldExit && !_resettingRunspace && !runspaceStateEventArgs.RunspaceStateInfo.IsUsable())
            {
                _resettingRunspace = true;
                PopOrReinitializeRunspaceAsync().HandleErrorsAsync(_logger);
            }
        }

        private Task PopOrReinitializeRunspaceAsync()
        {
            _cancellationContext.CancelCurrentTaskStack();
            RunspaceStateInfo oldRunspaceState = CurrentPowerShell.Runspace.RunspaceStateInfo;

            // Rather than try to lock the PowerShell executor while we alter its state,
            // we simply run this on its thread, guaranteeing that no other action can occur
            return ExecuteDelegateAsync(
                nameof(PopOrReinitializeRunspaceAsync),
                new ExecutionOptions { InterruptCurrentForeground = true },
                (cancellationToken) =>
                {
                    while (_psFrameStack.Count > 0
                        && !_psFrameStack.Peek().PowerShell.Runspace.RunspaceStateInfo.IsUsable())
                    {
                        PopPowerShell(RunspaceChangeAction.Shutdown);
                    }

                    _resettingRunspace = false;

                    if (_psFrameStack.Count == 0)
                    {
                        // If our main runspace was corrupted,
                        // we must re-initialize our state.
                        // TODO: Use runspace.ResetRunspaceState() here instead
                        (PowerShell pwsh, RunspaceInfo runspaceInfo, EngineIntrinsics engineIntrinsics) = CreateInitialPowerShellSession();
                        _mainRunspaceEngineIntrinsics = engineIntrinsics;
                        PushPowerShell(new PowerShellContextFrame(pwsh, runspaceInfo, PowerShellFrameType.Normal));

                        _logger.LogError($"Top level runspace entered state '{oldRunspaceState.State}' for reason '{oldRunspaceState.Reason}' and was reinitialized."
                            + " Please report this issue in the PowerShell/vscode-PowerShell GitHub repository with these logs.");
                        UI.WriteErrorLine("The main runspace encountered an error and has been reinitialized. See the PowerShell extension logs for more details.");
                    }
                    else
                    {
                        _logger.LogError($"Current runspace entered state '{oldRunspaceState.State}' for reason '{oldRunspaceState.Reason}' and was popped.");
                        UI.WriteErrorLine($"The current runspace entered state '{oldRunspaceState.State}' for reason '{oldRunspaceState.Reason}'."
                            + " If this occurred when using Ctrl+C in a Windows PowerShell remoting session, this is expected behavior."
                            + " The session is now returning to the previous runspace.");
                    }
                },
                CancellationToken.None);
        }

        private record RunspaceFrame(
            Runspace Runspace,
            RunspaceInfo RunspaceInfo);
    }
}
