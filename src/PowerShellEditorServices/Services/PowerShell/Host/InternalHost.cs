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

        private readonly PowerShellFactory _psFactory;

        private readonly EditorServicesConsolePSHost _publicHost;

        private readonly ReadLineProvider _readLineProvider;

        private readonly PowerShellExecutor _executor;

        private bool _shouldExit = false;

        private string _localComputerName;

        private ConsoleKeyInfo? _lastKey;

        public InternalHost(
            ILoggerFactory loggerFactory,
            ILanguageServerFacade languageServer,
            HostStartupInfo hostInfo,
            EditorServicesConsolePSHost publicHost)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<InternalHost>();
            _languageServer = languageServer;
            _hostInfo = hostInfo;

            _taskQueue = new BlockingConcurrentDeque<ISynchronousTask>();
            _psFrameStack = new Stack<PowerShellContextFrame>();
            _runspaceStack = new Stack<(Runspace, RunspaceInfo)>();
            _psFactory = new PowerShellFactory(loggerFactory, this);
            _executor = new PowerShellExecutor(loggerFactory, publicHost);

            PublicHost = publicHost;
            Name = hostInfo.Name;
            Version = hostInfo.Version;

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

        IRunspaceInfo IRunspaceContext.CurrentRunspace => CurrentRunspace;

        private PowerShellContextFrame CurrentFrame => _psFrameStack.Peek();

        public override void EnterNestedPrompt()
        {
            PushPowerShellAndRunLoop(_psFactory.CreateNestedPowerShell(CurrentRunspace), PowerShellFrameType.Nested);
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
            PushPowerShellAndRunLoop(_psFactory.CreatePowerShellForRunspace(runspace), PowerShellFrameType.Remote);
        }

        public override void SetShouldExit(int exitCode)
        {
            // TODO: Handle exit code if needed
            SetExit();
        }

        public void Start()
        {
            SMA.PowerShell pwsh = CreateInitialPowerShell(_hostInfo, _readLineProvider);
            PushPowerShellAndRunLoop(pwsh, PowerShellFrameType.Normal);
        }

        private void SetExit()
        {
            if (_psFrameStack.Count <= 1)
            {
                return;
            }

            _shouldExit = true;
        }

        private void PushPowerShellAndRunLoop(SMA.PowerShell pwsh, PowerShellFrameType frameType)
        {
            RunspaceInfo runspaceInfo = null;
            if (_runspaceStack.Count > 0)
            {
                // This is more than just an optimization.
                // When debugging, we cannot execute PowerShell directly to get this information;
                // trying to do so will block on the command that called us, deadlocking execution.
                // Instead, since we are reusing the runspace, we reuse that runspace's info as well.
                (Runspace currentRunspace, RunspaceInfo currentRunspaceInfo) = _runspaceStack.Peek();
                if (currentRunspace == pwsh.Runspace)
                {
                    runspaceInfo = currentRunspaceInfo;
                }
            }

            if (runspaceInfo is null)
            {
                RunspaceOrigin runspaceOrigin = pwsh.Runspace.RunspaceIsRemote ? RunspaceOrigin.EnteredProcess : RunspaceOrigin.Local;
                runspaceInfo = RunspaceInfo.CreateFromPowerShell(_logger, pwsh, runspaceOrigin, _localComputerName);
                _runspaceStack.Push((pwsh.Runspace, runspaceInfo));
            }

            // TODO: Improve runspace origin detection here
            PushPowerShellAndRunLoop(new PowerShellContextFrame(pwsh, runspaceInfo, frameType));
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
                RunExecutionLoop();
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
                RemoveRunspaceEventHandlers(frame.PowerShell.Runspace);

                if (_runspaceStack.Peek().Item1 != CurrentPowerShell.Runspace)
                {
                    _runspaceStack.Pop();
                }
            }
            finally
            {
                frame.Dispose();
            }
        }

        private void RunExecutionLoop()
        {
            while (true)
            {
                DoOneRepl(CancellationToken.None);

                if (_shouldExit)
                {
                    break;
                }

                while (_taskQueue.TryTake(out ISynchronousTask task))
                {
                    RunTaskSynchronously(task, CancellationToken.None);
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
            IReadOnlyList<string> results = _executor.InvokePSCommand<string>(command, PowerShellExecutionOptions.Default, cancellationToken);
            return results.Count > 0 ? results[0] : null;
        }

        private string InvokeReadLine(CancellationToken cancellationToken)
        {
            return _readLineProvider.ReadLine.ReadLine(cancellationToken);
        }

        private void InvokeInput(string input, CancellationToken cancellationToken)
        {
            var command = new PSCommand().AddScript(input, useLocalScope: false);
            _executor.InvokePSCommand(command, new PowerShellExecutionOptions { AddToHistory = true, WriteErrorsToHost = true, WriteOutputToHost = true }, cancellationToken);
        }

        private void RunTaskSynchronously(ISynchronousTask task, CancellationToken cancellationToken)
        {
            if (task.IsCanceled)
            {
                return;
            }

            task.ExecuteSynchronously(cancellationToken);
        }

        private IReadOnlyList<TResult> RunPSCommandSynchronously<TResult>(PSCommand psCommand, PowerShellExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            var task = new SynchronousPowerShellTask<TResult>(_logger, _publicHost, psCommand, executionOptions, cancellationToken);
            task.ExecuteSynchronously(cancellationToken);
            return task.Result;
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
                var readLine = new ConsoleReadLine(psrlProxy, this, _executor, engineIntrinsics);
                _readLineProvider.ReadLine.TryOverrideReadKey(ReadKey);
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

            Runspace runspace = RunspaceFactory.CreateRunspace(_publicHost, iss);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;

            runspace.Open();

            Runspace.DefaultRunspace = runspace;

            return runspace;
        }

        private void OnPowerShellIdle()
        {

        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {

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
                PushPowerShellAndRunLoop(_psFactory.CreateNestedPowerShell(CurrentRunspace), PowerShellFrameType.Debug | PowerShellFrameType.Nested);
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

    }
}
