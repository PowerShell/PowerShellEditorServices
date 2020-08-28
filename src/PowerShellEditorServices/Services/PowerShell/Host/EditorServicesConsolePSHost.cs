using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Threading;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    using System.Management.Automation.Runspaces;

    internal class EditorServicesConsolePSHost : PSHost, IHostSupportsInteractiveSession, IRunspaceContext
    {
        private readonly ILogger _logger;

        private readonly Stack<PowerShellContextFrame> _psFrameStack;

        private readonly PowerShellFactory _psFactory;

        private readonly ConsoleReplRunner _consoleReplRunner;

        private readonly PipelineThreadExecutor _pipelineExecutor;

        private readonly HostStartupInfo _hostInfo;

        private readonly ReadLineProvider _readLineProvider;

        private readonly Stack<KeyValuePair<Runspace, RunspaceInfo>> _runspacesInUse;

        private string _localComputerName;

        private int _hostStarted = 0;

        public EditorServicesConsolePSHost(
            ILoggerFactory loggerFactory,
            ILanguageServer languageServer,
            HostStartupInfo hostInfo)
        {
            _logger = loggerFactory.CreateLogger<EditorServicesConsolePSHost>();
            _psFrameStack = new Stack<PowerShellContextFrame>();
            _psFactory = new PowerShellFactory(loggerFactory, this);
            _runspacesInUse = new Stack<KeyValuePair<Runspace, RunspaceInfo>>();
            _hostInfo = hostInfo;
            Name = hostInfo.Name;
            Version = hostInfo.Version;

            _readLineProvider = new ReadLineProvider(loggerFactory);
            _pipelineExecutor = new PipelineThreadExecutor(loggerFactory, hostInfo, this, _readLineProvider);
            ExecutionService = new PowerShellExecutionService(loggerFactory, this, _pipelineExecutor);
            UI = new EditorServicesConsolePSHostUserInterface(loggerFactory, _readLineProvider, hostInfo.PSHost.UI);

            if (hostInfo.ConsoleReplEnabled)
            {
                _consoleReplRunner = new ConsoleReplRunner(loggerFactory, this, _readLineProvider, ExecutionService);
            }

            DebugContext = new PowerShellDebugContext(loggerFactory, languageServer, this,  _consoleReplRunner);
        }

        public override CultureInfo CurrentCulture => _hostInfo.PSHost.CurrentCulture;

        public override CultureInfo CurrentUICulture => _hostInfo.PSHost.CurrentUICulture;

        public override Guid InstanceId { get; } = Guid.NewGuid();

        public override string Name { get; }

        public override PSHostUserInterface UI { get; }

        public override Version Version { get; }

        public bool IsRunspacePushed { get; private set; }

        internal bool IsRunning => _hostStarted != 0;

        public Runspace Runspace => CurrentPowerShell.Runspace;

        internal string InitialWorkingDirectory { get; private set; }

        internal PowerShellExecutionService ExecutionService { get; }

        internal PowerShellDebugContext DebugContext { get; }

        internal SMA.PowerShell CurrentPowerShell => CurrentFrame.PowerShell;

        internal RunspaceInfo CurrentRunspace => CurrentFrame.RunspaceInfo;

        IRunspaceInfo IRunspaceContext.CurrentRunspace => CurrentRunspace;

        internal CancellationTokenSource CurrentCancellationSource => CurrentFrame.CancellationTokenSource;

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
            SetExit();
        }

        public void PushInitialPowerShell()
        {
            SMA.PowerShell pwsh = _psFactory.CreateInitialPowerShell(_hostInfo, _readLineProvider);
            var runspaceInfo = RunspaceInfo.CreateFromLocalPowerShell(_logger, pwsh);
            _localComputerName = runspaceInfo.SessionDetails.ComputerName;
            PushPowerShell(new PowerShellContextFrame(pwsh, runspaceInfo, PowerShellFrameType.Normal));
        }

        internal void PushNonInteractivePowerShell()
        {
            PushPowerShellAndRunLoop(_psFactory.CreateNestedPowerShell(CurrentRunspace), PowerShellFrameType.Nested | PowerShellFrameType.NonInteractive);
        }

        internal void CancelCurrentPrompt()
        {
            _consoleReplRunner?.CancelCurrentPrompt();
        }

        internal void StartRepl()
        {
            _consoleReplRunner?.StartRepl();
        }

        internal void PushNewReplTask()
        {
            _consoleReplRunner?.PushNewReplTask();
        }

        internal Task SetInitialWorkingDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            InitialWorkingDirectory = path;

            return ExecutionService.ExecutePSCommandAsync(
                new PSCommand().AddCommand("Set-Location").AddParameter("LiteralPath", path),
                new PowerShellExecutionOptions(),
                cancellationToken);
        }

        public async Task StartAsync(HostStartOptions hostStartOptions, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Host starting");
            if (Interlocked.Exchange(ref _hostStarted, 1) != 0)
            {
                _logger.LogDebug("Host start requested after already started");
                return;
            }

            _pipelineExecutor.Start();

            if (hostStartOptions.LoadProfiles)
            {
                await ExecutionService.ExecuteDelegateAsync((pwsh, delegateCancellation) =>
                {
                    pwsh.LoadProfiles(_hostInfo.ProfilePaths);
                }, "LoadProfiles", cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Profiles loaded");
            }

            if (hostStartOptions.InitialWorkingDirectory != null)
            {
                await SetInitialWorkingDirectoryAsync(hostStartOptions.InitialWorkingDirectory, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private void SetExit()
        {
            if (_psFrameStack.Count <= 1)
            {
                return;
            }

            _pipelineExecutor.IsExiting = true;

            if ((CurrentFrame.FrameType & PowerShellFrameType.NonInteractive) == 0)
            {
                _consoleReplRunner?.SetReplPop();
            }
        }

        private void PushPowerShellAndRunLoop(SMA.PowerShell pwsh, PowerShellFrameType frameType)
        {
            RunspaceInfo runspaceInfo = null;
            if (_runspacesInUse.Count > 0)
            {
                // This is more than just an optimization.
                // When debugging, we cannot execute PowerShell directly to get this information;
                // trying to do so will block on the command that called us, deadlocking execution.
                // Instead, since we are reusing the runspace, we reuse that runspace's info as well.
                KeyValuePair<Runspace, RunspaceInfo> currentRunspace = _runspacesInUse.Peek();
                if (currentRunspace.Key == pwsh.Runspace)
                {
                    runspaceInfo = currentRunspace.Value;
                }
            }

            if (runspaceInfo is null)
            {
                RunspaceOrigin runspaceOrigin = pwsh.Runspace.RunspaceIsRemote ? RunspaceOrigin.EnteredProcess : RunspaceOrigin.Local;
                runspaceInfo = RunspaceInfo.CreateFromPowerShell(_logger, pwsh, runspaceOrigin, _localComputerName);
                _runspacesInUse.Push(new KeyValuePair<Runspace, RunspaceInfo>(pwsh.Runspace, runspaceInfo));
            }

            // TODO: Improve runspace origin detection here
            PushPowerShellAndRunLoop(new PowerShellContextFrame(pwsh, runspaceInfo, frameType));
        }

        private void PushPowerShellAndRunLoop(PowerShellContextFrame frame)
        {
            PushPowerShell(frame);
            _pipelineExecutor.RunPowerShellLoop(frame.FrameType);
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

        internal void PopPowerShell()
        {
            _pipelineExecutor.IsExiting = false;
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
                PopOrReinitializeRunspace();
            }
        }

        private void PopOrReinitializeRunspace()
        {
            _consoleReplRunner?.SetReplPop();
            _pipelineExecutor.CancelCurrentTask();

            RunspaceStateInfo oldRunspaceState = CurrentPowerShell.Runspace.RunspaceStateInfo;
            using (_pipelineExecutor.TakeTaskWriterLock())
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
            }
        }

    }
}
