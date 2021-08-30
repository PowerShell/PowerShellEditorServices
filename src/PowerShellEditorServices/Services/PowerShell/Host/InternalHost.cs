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

    internal class InternalHost : PSHost, IHostSupportsInteractiveSession, IRunspaceContext
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

        private readonly Stack<(Runspace, RunspaceInfo)> _runspaceStack;

        private readonly CancellationContext _cancellationContext;

        private readonly ReadLineProvider _readLineProvider;

        private readonly Thread _pipelineThread;

        private bool _shouldExit = false;

        private int _isRunning = 0;

        private string _localComputerName;

        private ConsoleKeyInfo? _lastKey;

        public InternalHost(
            ILoggerFactory loggerFactory,
            ILanguageServerFacade languageServer,
            HostStartupInfo hostInfo)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<InternalHost>();
            _languageServer = languageServer;
            _hostInfo = hostInfo;

            _readLineProvider = new ReadLineProvider(loggerFactory);
            _taskQueue = new BlockingConcurrentDeque<ISynchronousTask>();
            _psFrameStack = new Stack<PowerShellContextFrame>();
            _runspaceStack = new Stack<(Runspace, RunspaceInfo)>();
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

        public Runspace Runspace => _runspaceStack.Peek().Item1;

        public RunspaceInfo CurrentRunspace => CurrentFrame.RunspaceInfo;

        public SMA.PowerShell CurrentPowerShell => CurrentFrame.PowerShell;

        public EditorServicesConsolePSHost PublicHost { get; }

        public PowerShellDebugContext DebugContext { get; }

        public bool IsRunning => _isRunning != 0;

        public string InitialWorkingDirectory { get; private set; }

        IRunspaceInfo IRunspaceContext.CurrentRunspace => CurrentRunspace;

        private PowerShellContextFrame CurrentFrame => _psFrameStack.Peek();

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
            if (Interlocked.Exchange(ref _isRunning, 1) != 0)
            {
                _logger.LogDebug("Host start requested after already started");
                return;
            }

            _pipelineThread.Start();

            if (startOptions.LoadProfiles)
            {
                await ExecuteDelegateAsync(
                   "LoadProfiles",
                   new PowerShellExecutionOptions { MustRunInForeground = true },
                   cancellationToken,
                    (pwsh, delegateCancellation) =>
                    {
                        pwsh.LoadProfiles(_hostInfo.ProfilePaths);
                    }).ConfigureAwait(false);

                _logger.LogInformation("Profiles loaded");
            }

            if (startOptions.InitialWorkingDirectory is not null)
            {
                await SetInitialWorkingDirectoryAsync(startOptions.InitialWorkingDirectory, CancellationToken.None).ConfigureAwait(false);
            }
        }

        public void SetExit()
        {
            if (_psFrameStack.Count <= 1)
            {
                return;
            }

            _shouldExit = true;
        }

        public Task<T> InvokeTaskOnPipelineThreadAsync<T>(
            SynchronousTask<T> task)
        {
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
            CancellationToken cancellationToken,
            Func<SMA.PowerShell, CancellationToken, TResult> func)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousPSDelegateTask<TResult>(_logger, this, representation, executionOptions ?? ExecutionOptions.Default, cancellationToken, func));
        }

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Action<SMA.PowerShell, CancellationToken> action)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousPSDelegateTask(_logger, this, representation, executionOptions ?? ExecutionOptions.Default, cancellationToken, action));
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Func<CancellationToken, TResult> func)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousDelegateTask<TResult>(_logger, representation, executionOptions ?? ExecutionOptions.Default, cancellationToken, func));
        }

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            CancellationToken cancellationToken,
            Action<CancellationToken> action)
        {
            return InvokeTaskOnPipelineThreadAsync(new SynchronousDelegateTask(_logger, representation, executionOptions ?? ExecutionOptions.Default, cancellationToken, action));
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
            var task = new SynchronousPowerShellTask<TResult>(_logger, this, psCommand, executionOptions, cancellationToken);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokePSCommand(PSCommand psCommand, PowerShellExecutionOptions executionOptions, CancellationToken cancellationToken)
            => InvokePSCommand<PSObject>(psCommand, executionOptions, cancellationToken);

        public TResult InvokePSDelegate<TResult>(string representation, ExecutionOptions executionOptions, Func<SMA.PowerShell, CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            var task = new SynchronousPSDelegateTask<TResult>(_logger, this, representation, executionOptions, cancellationToken, func);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokePSDelegate(string representation, ExecutionOptions executionOptions, Action<SMA.PowerShell, CancellationToken> action, CancellationToken cancellationToken)
        {
            var task = new SynchronousPSDelegateTask(_logger, this, representation, executionOptions, cancellationToken, action);
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
            SMA.PowerShell pwsh = CreateInitialPowerShell(_hostInfo, _readLineProvider);
            RunspaceInfo localRunspaceInfo = RunspaceInfo.CreateFromLocalPowerShell(_logger, pwsh);
            _runspaceStack.Push((pwsh.Runspace, localRunspaceInfo));
            PushPowerShellAndRunLoop(pwsh, PowerShellFrameType.Normal, localRunspaceInfo);
        }

        private void PushPowerShellAndRunLoop(SMA.PowerShell pwsh, PowerShellFrameType frameType, RunspaceInfo runspaceInfo = null)
        {
            // TODO: Improve runspace origin detection here
            if (runspaceInfo is null)
            {
                runspaceInfo = GetRunspaceInfoForPowerShell(pwsh, out bool isNewRunspace);

                if (isNewRunspace)
                {
                    _runspaceStack.Push((pwsh.Runspace, runspaceInfo));
                }
            }

            PushPowerShellAndRunLoop(new PowerShellContextFrame(pwsh, runspaceInfo, frameType));
        }

        private RunspaceInfo GetRunspaceInfoForPowerShell(SMA.PowerShell pwsh, out bool isNewRunspace)
        {
            if (_runspaceStack.Count > 0)
            {
                // This is more than just an optimization.
                // When debugging, we cannot execute PowerShell directly to get this information;
                // trying to do so will block on the command that called us, deadlocking execution.
                // Instead, since we are reusing the runspace, we reuse that runspace's info as well.
                (Runspace currentRunspace, RunspaceInfo currentRunspaceInfo) = _runspaceStack.Peek();
                if (currentRunspace == pwsh.Runspace)
                {
                    isNewRunspace = false;
                    return currentRunspaceInfo;
                }
            }

            RunspaceOrigin runspaceOrigin = pwsh.Runspace.RunspaceIsRemote ? RunspaceOrigin.EnteredProcess : RunspaceOrigin.Local;
            isNewRunspace = true;
            return RunspaceInfo.CreateFromPowerShell(_logger, pwsh, runspaceOrigin, _localComputerName);
        }

        private void PushPowerShellAndRunLoop(PowerShellContextFrame frame)
        {
            if (_psFrameStack.Count > 0)
            {
                RemoveRunspaceEventHandlers(CurrentFrame.PowerShell.Runspace);
            }

            AddRunspaceEventHandlers(frame.PowerShell.Runspace);

            _psFrameStack.Push(frame);

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

        private void PopPowerShell()
        {
            _shouldExit = false;
            PowerShellContextFrame frame = _psFrameStack.Pop();
            try
            {
                // If we're changing runspace, make sure we move the handlers over
                if (_runspaceStack.Peek().Item1 != CurrentPowerShell.Runspace)
                {
                    (Runspace parentRunspace, _) = _runspaceStack.Pop();
                    RemoveRunspaceEventHandlers(frame.PowerShell.Runspace);
                    AddRunspaceEventHandlers(parentRunspace);
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
            if (_psFrameStack.Count == 1)
            {
                while (_taskQueue.TryTake(out ISynchronousTask task))
                {
                    task.ExecuteSynchronously(CancellationToken.None);
                }
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

                    if (_shouldExit)
                    {
                        break;
                    }

                    while (!cancellationScope.CancellationToken.IsCancellationRequested
                        && _taskQueue.TryTake(out ISynchronousTask task))
                    {
                        task.ExecuteSynchronously(cancellationScope.CancellationToken);
                    }
                }
            }
        }

        private void DoOneRepl(CancellationToken cancellationToken)
        {
            try
            {
                string prompt = GetPrompt(cancellationToken) ?? DefaultPrompt;
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
            return results.Count > 0 ? results[0] : null;
        }

        private string InvokeReadLine(CancellationToken cancellationToken)
        {
            return _readLineProvider.ReadLine.ReadLine(cancellationToken);
        }

        private void InvokeInput(string input, CancellationToken cancellationToken)
        {
            var command = new PSCommand().AddScript(input, useLocalScope: false);
            InvokePSCommand(command, new PowerShellExecutionOptions { AddToHistory = true, WriteErrorsToHost = true, WriteOutputToHost = true }, cancellationToken);
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

        private PowerShell CreateNestedPowerShell(RunspaceInfo currentRunspace)
        {
            if (currentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                var remotePwsh = PowerShell.Create();
                remotePwsh.Runspace = currentRunspace.Runspace;
                return remotePwsh;
            }

            // PowerShell.CreateNestedPowerShell() sets IsNested but not IsChild
            // This means it throws due to the parent pipeline not running...
            // So we must use the RunspaceMode.CurrentRunspace option on PowerShell.Create() instead
            var pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace);
            pwsh.Runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            return pwsh;
        }

        private PowerShell CreatePowerShellForRunspace(Runspace runspace)
        {
            var pwsh = PowerShell.Create();
            pwsh.Runspace = runspace;
            return pwsh;
        }

        public PowerShell CreateInitialPowerShell(
            HostStartupInfo hostStartupInfo,
            ReadLineProvider readLineProvider)
        {
            Runspace runspace = CreateInitialRunspace(hostStartupInfo.LanguageMode);

            var pwsh = PowerShell.Create();
            pwsh.Runspace = runspace;

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

            return pwsh;
        }

        private Runspace CreateInitialRunspace(PSLanguageMode languageMode)
        {
            InitialSessionState iss = Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1"
                ? InitialSessionState.CreateDefault()
                : InitialSessionState.CreateDefault2();

            iss.LanguageMode = languageMode;

            Runspace runspace = RunspaceFactory.CreateRunspace(PublicHost, iss);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;

            runspace.Open();

            Runspace.DefaultRunspace = runspace;

            return runspace;
        }

        private void OnPowerShellIdle()
        {
            if (_taskQueue.Count == 0)
            {
                return;
            }

            using (CancellationScope cancellationScope = _cancellationContext.EnterScope(isIdleScope: true))
            {
                while (!cancellationScope.CancellationToken.IsCancellationRequested
                    && _taskQueue.TryTake(out ISynchronousTask task))
                {
                    task.ExecuteSynchronously(cancellationScope.CancellationToken);
                }
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
                && (_lastKey.Value.Modifiers & ConsoleModifiers.Alt) != 0;
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
            if (!runspaceStateEventArgs.RunspaceStateInfo.IsUsable())
            {
                //PopOrReinitializeRunspaceAsync();
            }
        }

        /*
        private void PopOrReinitializeRunspace()
        {
            SetExit();
            RunspaceStateInfo oldRunspaceState = CurrentPowerShell.Runspace.RunspaceStateInfo;

            // Rather than try to lock the PowerShell executor while we alter its state,
            // we simply run this on its thread, guaranteeing that no other action can occur
            _executor.InvokeDelegate(
                nameof(PopOrReinitializeRunspace),
                new ExecutionOptions { InterruptCurrentForeground = true },
                (cancellationToken) =>
                {
                    while (_psFrameStack.Count > 0
                        && !_psFrameStack.Peek().PowerShell.Runspace.RunspaceStateInfo.IsUsable())
                    {
                        PopPowerShell();
                    }

                    if (_psFrameStack.Count == 0)
                    {
                        // If our main runspace was corrupted,
                        // we must re-initialize our state.
                        // TODO: Use runspace.ResetRunspaceState() here instead
                        PushInitialPowerShell();

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
        */
    }
}
