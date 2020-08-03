using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
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
    internal class PowerShellExecutionService : IDisposable
    {
        private static readonly string s_commandsModulePath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "../../Commands/PowerShellEditorServices.Commands.psd1"));

        private static readonly PropertyInfo s_shouldProcessInExecutionThreadProperty =
            typeof(PSEventSubscriber)
                .GetProperty(
                    "ShouldProcessInExecutionThread",
                    BindingFlags.Instance | BindingFlags.NonPublic);


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

        private readonly CancellationTokenSource _consumerThreadCancellationSource;

        private readonly BlockingCollection<ISynchronousTask> _executionQueue;

        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        private readonly ILanguageServer _languageServer;

        private readonly ConsoleReplRunner _consoleRepl;

        private readonly string _hostName;

        private readonly Version _hostVersion;

        private readonly PSLanguageMode _languageMode;

        private readonly PSHost _internalHost;

        private readonly ProfilePathInfo _profilePaths;

        private readonly IReadOnlyList<string> _additionalModulesToLoad;

        private readonly DebuggingContext _debuggingContext;

        private readonly Stack<PowerShellContextFrame> _psFrameStack;

        private Thread _pipelineThread;

        private readonly ConcurrentStack<CancellationTokenSource> _loopCancellationStack;

        private readonly ConcurrentStack<CancellationTokenSource> _commandCancellationStack;

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

        public void Stop()
        {
            _consumerThreadCancellationSource.Cancel();
            _pipelineThread.Join();
        }

        public void CancelCurrentTask()
        {
            if (_commandCancellationStack.TryPeek(out CancellationTokenSource currentCommandCancellation))
            {
                currentCommandCancellation.Cancel();
            }
        }

        public void Dispose()
        {
            Stop();
            while (_psFrameStack.Count > 0)
            {
                PopFrame();
            }
        }

        private Task<T> QueueTask<T>(SynchronousTask<T> task)
        {
            _executionQueue.Add(task);
            return task.Task;
        }

        private void Start()
        {
            _pipelineThread = new Thread(RunTopLevelConsumerLoop)
            {
                Name = "PSES Execution Service Thread",
            };
            _pipelineThread.SetApartmentState(ApartmentState.STA);
            _pipelineThread.Start();
        }

        private void RunTopLevelConsumerLoop()
        {
            Initialize();

            _consoleRepl.StartRepl();

            var cancellationContext = LoopCancellationContext.EnterNew(
                this,
                CurrentPowerShellCancellationSource,
                _consumerThreadCancellationSource);
            try
            {
                foreach (ISynchronousTask task in _executionQueue.GetConsumingEnumerable(cancellationContext.CancellationToken))
                {
                    RunTaskSynchronously(task, cancellationContext.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Catch cancellations to end nicely
            }
            finally
            {
                cancellationContext.Dispose();
            }
        }

        private void RunTaskSynchronously(ISynchronousTask task, CancellationToken loopCancellationToken)
        {
            if (task.IsCanceled)
            {
                return;
            }

            using (var cancellationContext = TaskCancellationContext.EnterNew(this, loopCancellationToken))
            {
                task.ExecuteSynchronously(cancellationContext.CancellationToken);
            }
        }

        private void Initialize()
        {
            ReadLine = new ConsoleReadLine();

            EditorServicesHost = new EditorServicesConsolePSHost(
                _loggerFactory,
                _hostName,
                _hostVersion,
                _internalHost,
                ReadLine);

            PushInitialRunspace(EditorServicesHost, _languageMode);

            EditorServicesHost.RegisterPowerShellContext(new PowerShellRunspaceContext(this));

            EngineIntrinsics = (EngineIntrinsics)CurrentPowerShell.Runspace.SessionStateProxy.GetVariable("ExecutionContext");

            PSReadLineProxy = PSReadLineProxy.LoadAndCreate(_loggerFactory, CurrentPowerShell);
            PSReadLineProxy.OverrideIdleHandler(OnPowerShellIdle);
            ReadLine.RegisterExecutionDependencies(this, PSReadLineProxy);

            if (VersionUtils.IsWindows)
            {
                SetExecutionPolicy();
            }

            LoadProfiles();

            ImportModule(s_commandsModulePath);

            if (_additionalModulesToLoad != null && _additionalModulesToLoad.Count > 0)
            {
                foreach (string module in _additionalModulesToLoad)
                {
                    ImportModule(module);
                }
            }
        }

        private void SetExecutionPolicy()
        {
            // We want to get the list hierarchy of execution policies
            // Calling the cmdlet is the simplest way to do that
            IReadOnlyList<PSObject> policies = CurrentPowerShell
                .AddCommand("Microsoft.PowerShell.Security\\Get-ExecutionPolicy")
                    .AddParameter("-List")
                .InvokeAndClear<PSObject>();

            // The policies come out in the following order:
            // - MachinePolicy
            // - UserPolicy
            // - Process
            // - CurrentUser
            // - LocalMachine
            // We want to ignore policy settings, since we'll already have those anyway.
            // Then we need to look at the CurrentUser setting, and then the LocalMachine setting.
            //
            // Get-ExecutionPolicy -List emits PSObjects with Scope and ExecutionPolicy note properties
            // set to expected values, so we must sift through those.

            ExecutionPolicy policyToSet = ExecutionPolicy.Bypass;
            var currentUserPolicy = (ExecutionPolicy)policies[policies.Count - 2].Members["ExecutionPolicy"].Value;
            if (currentUserPolicy != ExecutionPolicy.Undefined)
            {
                policyToSet = currentUserPolicy;
            }
            else
            {
                var localMachinePolicy = (ExecutionPolicy)policies[policies.Count - 1].Members["ExecutionPolicy"].Value;
                if (localMachinePolicy != ExecutionPolicy.Undefined)
                {
                    policyToSet = localMachinePolicy;
                }
            }

            // If there's nothing to do, save ourselves a PowerShell invocation
            if (policyToSet == ExecutionPolicy.Bypass)
            {
                _logger.LogTrace("Execution policy already set to Bypass. Skipping execution policy set");
                return;
            }

            // Finally set the inherited execution policy
            _logger.LogTrace("Setting execution policy to {Policy}", policyToSet);
            try
            {
                CurrentPowerShell.AddCommand("Microsoft.PowerShell.Security\\Set-ExecutionPolicy")
                    .AddParameter("Scope", ExecutionPolicyScope.Process)
                    .AddParameter("ExecutionPolicy", policyToSet)
                    .AddParameter("Force")
                    .InvokeAndClear();
            }
            catch (CmdletInvocationException e)
            {
                _logger.LogError(e, "Error occurred calling 'Set-ExecutionPolicy -Scope Process -ExecutionPolicy {Policy} -Force'", policyToSet);
            }
        }

        private void LoadProfiles()
        {
            var profileVariable = new PSObject();

            AddProfileMemberAndLoadIfExists(profileVariable, nameof(_profilePaths.AllUsersAllHosts), _profilePaths.AllUsersAllHosts);
            AddProfileMemberAndLoadIfExists(profileVariable, nameof(_profilePaths.AllUsersCurrentHost), _profilePaths.AllUsersCurrentHost);
            AddProfileMemberAndLoadIfExists(profileVariable, nameof(_profilePaths.CurrentUserAllHosts), _profilePaths.CurrentUserAllHosts);
            AddProfileMemberAndLoadIfExists(profileVariable, nameof(_profilePaths.CurrentUserCurrentHost), _profilePaths.CurrentUserCurrentHost);

            CurrentPowerShell.Runspace.SessionStateProxy.SetVariable("PROFILE", profileVariable);
        }

        private void AddProfileMemberAndLoadIfExists(PSObject profileVariable, string profileName, string profilePath)
        {
            profileVariable.Members.Add(new PSNoteProperty(profileName, profilePath));

            if (File.Exists(profilePath))
            {
                var psCommand = new PSCommand()
                    .AddScript(profilePath, useLocalScope: false)
                    .AddOutputCommand();

                CurrentPowerShell.InvokeCommand(psCommand);
            }
        }

        private void ImportModule(string moduleNameOrPath)
        {
            CurrentPowerShell.AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                .AddParameter("-Name", moduleNameOrPath)
                .InvokeAndClear();
        }

        private void PushInitialRunspace(EditorServicesConsolePSHost psHost, PSLanguageMode languageMode)
        {
            InitialSessionState iss = Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1"
                ? InitialSessionState.CreateDefault()
                : InitialSessionState.CreateDefault2();

            iss.LanguageMode = languageMode;

            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, iss);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;

            runspace.Open();

            AddRunspaceEventHandlers(runspace);

            var pwsh = SMA.PowerShell.Create();
            pwsh.Runspace = runspace;
            _psFrameStack.Push(new PowerShellContextFrame(pwsh, PowerShellFrameType.Normal, new CancellationTokenSource()));

            Runspace.DefaultRunspace = runspace;
        }

        private void RunNestedLoop(in LoopCancellationContext cancellationContext)
        {
            try
            {
                foreach (ISynchronousTask task in _executionQueue.GetConsumingEnumerable(cancellationContext.CancellationToken))
                {
                    RunTaskSynchronously(task, cancellationContext.CancellationToken);

                    if (_isExiting)
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

        private void RunDebugLoop(in LoopCancellationContext cancellationContext)
        {
            // If the debugger is resumed while the execution queue listener is blocked on getting a new execution event,
            // we must cancel the blocking call
            var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_debuggingContext.DebuggerResumeCancellationToken, cancellationContext.CancellationToken);

            try
            {
                DebuggerStopped?.Invoke(this, _debuggingContext.LastStopEventArgs);

                // Run commands, but cancelling our blocking wait if the debugger resumes
                foreach (ISynchronousTask task in _executionQueue.GetConsumingEnumerable(cancellationSource.Token))
                {
                    // We don't want to cancel the current command when the debugger resumes,
                    // since that command will be resuming the debugger.
                    // Instead let it complete and check the cancellation afterward.
                    RunTaskSynchronously(task, cancellationContext.CancellationToken);

                    if (cancellationSource.Token.IsCancellationRequested)
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
                _debuggingContext.ResetCurrentStopContext();
                cancellationSource.Dispose();
            }
        }

        private void RunIdleLoop(in LoopCancellationContext cancellationContext)
        {
            try
            {
                while (_executionQueue.TryTake(out ISynchronousTask task))
                {
                    RunTaskSynchronously(task, cancellationContext.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {

            }

            // TODO: Run nested pipeline here for engine event handling
        }

        private void OnPowerShellIdle()
        {
            if (_executionQueue.Count == 0)
            {
                return;
            }

            _runIdleLoop = true;
            PushNonInteractivePowerShell();
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

        private void AddRunspaceEventHandlers(Runspace runspace)
        {
            runspace.Debugger.DebuggerStop += OnDebuggerStopped;
            runspace.Debugger.BreakpointUpdated += OnBreakpointUpdated;
        }

        private void RemoveRunspaceEventHandlers(Runspace runspace)
        {
            runspace.Debugger.DebuggerStop -= OnDebuggerStopped;
            runspace.Debugger.BreakpointUpdated -= OnBreakpointUpdated;
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

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
        {
            _debuggingContext.OnDebuggerStop(sender, debuggerStopEventArgs);
            PushDebugPowerShell();
        }

        private void SetDebuggerResuming(DebuggerResumeAction resumeAction)
        {
            _consoleRepl.SetReplPop();
            _debuggingContext.SetDebuggerResuming(resumeAction);
            DebuggerResuming?.Invoke(this, new DebuggerResumingEventArgs(resumeAction));
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs breakpointUpdatedEventArgs)
        {
            BreakpointUpdated?.Invoke(this, breakpointUpdatedEventArgs);
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
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(loopCancellationToken);
                executionService._commandCancellationStack.Push(cancellationTokenSource);
                return new TaskCancellationContext(executionService._commandCancellationStack, cancellationTokenSource.Token);
            }

            private TaskCancellationContext(ConcurrentStack<CancellationTokenSource> commandCancellationStack, CancellationToken cancellationToken)
            {
                _commandCancellationStack = commandCancellationStack;
                CancellationToken = cancellationToken;
            }

            private readonly ConcurrentStack<CancellationTokenSource> _commandCancellationStack;

            public readonly CancellationToken CancellationToken;

            public void Dispose()
            {
                if (_commandCancellationStack.TryPop(out CancellationTokenSource taskCancellation))
                {
                    taskCancellation.Dispose();
                }
            }
        }

        private class PowerShellContextFrame : IDisposable
        {
            private bool disposedValue;

            public PowerShellContextFrame(SMA.PowerShell powerShell, PowerShellFrameType frameType, CancellationTokenSource cancellationTokenSource)
            {
                PowerShell = powerShell;
                FrameType = frameType;
                CancellationTokenSource = cancellationTokenSource;
            }

            public SMA.PowerShell PowerShell { get; }

            public PowerShellFrameType FrameType { get; }

            public CancellationTokenSource CancellationTokenSource { get; }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        PowerShell.Dispose();
                        CancellationTokenSource.Dispose();
                    }

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}
