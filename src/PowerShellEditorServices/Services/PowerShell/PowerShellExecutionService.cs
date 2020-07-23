using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Language;
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

        private readonly string _hostName;

        private readonly Version _hostVersion;

        private readonly PSLanguageMode _languageMode;

        private readonly PSHost _internalHost;

        private readonly ProfilePathInfo _profilePaths;

        private readonly IReadOnlyList<string> _additionalModulesToLoad;

        private readonly DebuggingContext _debuggingContext;

        private Thread _pipelineThread;

        private ConcurrentStack<CancellationTokenSource> _loopCancellationStack;

        private ConcurrentStack<CancellationTokenSource> _commandCancellationStack;

        private Execution.PowerShellContext _pwshContext;

        private bool _exitNestedPrompt;

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
            _consumerThreadCancellationSource = new CancellationTokenSource();
            _loopCancellationStack = new ConcurrentStack<CancellationTokenSource>();
            _commandCancellationStack = new ConcurrentStack<CancellationTokenSource>();
            _executionQueue = new BlockingCollection<ISynchronousTask>();
            _debuggingContext = new DebuggingContext();
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

        public event Action<object, PromptFramePushedArgs> PromptFramePushed;

        public event Action<object, PromptFramePoppedArgs> PromptFramePopped;

        public event Action<object, DebuggerStopEventArgs> DebuggerStopped;

        public event Action<object, DebuggerResumedArgs> DebuggerResumed;

        public event Action<object, BreakpointUpdatedEventArgs> BreakpointUpdated;

        public event Action<object, PromptCancellationRequestedArgs> PromptCancellationRequested;

        public event Action<object, NestedPromptExitedArgs> NestedPromptExited;

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
        {
            TResult appliedFunc(CancellationToken cancellationToken) => func(_pwshContext.CurrentPowerShell, cancellationToken);
            return ExecuteDelegateAsync(appliedFunc, representation, cancellationToken);
        }

        public Task ExecuteDelegateAsync(
            Action<SMA.PowerShell, CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
        {
            void appliedAction(CancellationToken cancellationToken) => action(_pwshContext.CurrentPowerShell, cancellationToken);
            return ExecuteDelegateAsync(appliedAction, representation, cancellationToken);
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
            Task<IReadOnlyList<TResult>> result = QueueTask(new SynchronousPowerShellTask<TResult>(_logger, _pwshContext, EditorServicesHost, psCommand, executionOptions, cancellationToken));

            if (executionOptions.InterruptCommandPrompt)
            {
                PromptCancellationRequested?.Invoke(this, new PromptCancellationRequestedArgs());
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

        public void EnterNestedPrompt()
        {
            _pwshContext.PushNestedPowerShell();
            var cancellationContext = LoopCancellationContext.EnterNew(this, _pwshContext.CurrentCancellationSource, _consumerThreadCancellationSource);
            try
            {
                foreach (ISynchronousTask task in _executionQueue.GetConsumingEnumerable(cancellationContext.CancellationToken))
                {
                    RunTaskSynchronously(task, cancellationContext.CancellationToken);

                    if (_exitNestedPrompt)
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
                _exitNestedPrompt = false;
                cancellationContext.Dispose();
                _pwshContext.PopPowerShell();
            }
        }

        public void ExitNestedPrompt()
        {
            NestedPromptExited?.Invoke(this, new NestedPromptExitedArgs());
            _exitNestedPrompt = true;
        }

        public void Dispose()
        {
            Stop();
            _pwshContext.Dispose();
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

        private void Initialize()
        {
            ReadLine = new ConsoleReadLine();

            EditorServicesHost = new EditorServicesConsolePSHost(_loggerFactory, this, _hostName, _hostVersion, _internalHost, ReadLine);

            SetPowerShellContext(EditorServicesHost, _languageMode);

            EngineIntrinsics = (EngineIntrinsics)_pwshContext.CurrentPowerShell.Runspace.SessionStateProxy.GetVariable("ExecutionContext");

            PSReadLineProxy = PSReadLineProxy.LoadAndCreate(_loggerFactory, _pwshContext.CurrentPowerShell);
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

        private void RunTopLevelConsumerLoop()
        {
            Initialize();

            var cancellationContext = LoopCancellationContext.EnterNew(
                this,
                _pwshContext.CurrentCancellationSource,
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

        private void OnPowerShellIdle()
        {
            if (_executionQueue.Count == 0)
            {
                return;
            }

            var loopCancellationContext = LoopCancellationContext.EnterNew(
                this,
                _pwshContext.CurrentCancellationSource,
                _consumerThreadCancellationSource);

            try
            {
                while (_pwshContext.CurrentPowerShell.InvocationStateInfo.State == PSInvocationState.Completed
                    && _executionQueue.TryTake(out ISynchronousTask task))
                {
                    RunTaskSynchronously(task, loopCancellationContext.CancellationToken);
                }
            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                loopCancellationContext.Dispose();
            }

            // TODO: Run nested pipeline here for engine event handling
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

        private void SetExecutionPolicy()
        {
            // We want to get the list hierarchy of execution policies
            // Calling the cmdlet is the simplest way to do that
            IReadOnlyList<PSObject> policies = _pwshContext.CurrentPowerShell
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
                _pwshContext.CurrentPowerShell.AddCommand("Microsoft.PowerShell.Security\\Set-ExecutionPolicy")
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

            _pwshContext.CurrentPowerShell.Runspace.SessionStateProxy.SetVariable("PROFILE", profileVariable);
        }

        private void AddProfileMemberAndLoadIfExists(PSObject profileVariable, string profileName, string profilePath)
        {
            profileVariable.Members.Add(new PSNoteProperty(profileName, profilePath));

            if (File.Exists(profilePath))
            {
                var psCommand = new PSCommand()
                    .AddScript(profilePath, useLocalScope: false)
                    .AddOutputCommand();

                _pwshContext.CurrentPowerShell.InvokeCommand(psCommand);
            }
        }

        private void ImportModule(string moduleNameOrPath)
        {
            _pwshContext.CurrentPowerShell.AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                .AddParameter("-Name", moduleNameOrPath)
                .InvokeAndClear();
        }

        private void SetPowerShellContext(EditorServicesConsolePSHost psHost, PSLanguageMode languageMode)
        {
            _pwshContext = Execution.PowerShellContext.Create(psHost, languageMode);
            _pwshContext.PromptFramePushed += OnPromptFramePushed;
            _pwshContext.PromptFramePopped += OnPromptFramePopped;
            _pwshContext.DebuggerStopped += OnDebuggerStopped;
            _pwshContext.DebuggerResumed += OnDebuggerResumed;
            _pwshContext.BreakpointUpdated += OnBreakpointUpdated;
        }

        private void OnPromptFramePushed(object sender, PromptFramePushedArgs promptFramePushedArgs)
        {
            PromptFramePushed?.Invoke(this, promptFramePushedArgs);
        }

        private void OnPromptFramePopped(object sender, PromptFramePoppedArgs promptFramePoppedArgs)
        {
            PromptFramePopped?.Invoke(this, promptFramePoppedArgs);
        }

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
        {
            _debuggingContext.OnDebuggerStop(sender, debuggerStopEventArgs);

            _pwshContext.PushNestedPowerShell();

            DebuggerStopped?.Invoke(this, debuggerStopEventArgs);

            var cancellationContext = LoopCancellationContext.EnterNew(
                this,
                _pwshContext.CurrentCancellationSource,
                _consumerThreadCancellationSource);

            // If the debugger is resumed while the execution queue listener is blocked on getting a new execution event,
            // we must cancel the blocking call
            var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_debuggingContext.DebuggerResumeCancellationToken.Value, cancellationContext.CancellationToken);

            try
            {
                // Run commands, but cancelling our blocking wait if the debugger resumes
                foreach (ISynchronousTask task in _executionQueue.GetConsumingEnumerable(cancellationSource.Token))
                {
                    // We don't want to cancel the current command when the debugger resumes,
                    // since that command will be resuming the debugger.
                    // Instead let it complete and check the cancellation afterward.
                    RunTaskSynchronously(task, cancellationContext.CancellationToken);

                    if (_debuggingContext.LastResumeAction != null)
                    {
                        debuggerStopEventArgs.ResumeAction = _debuggingContext.LastResumeAction.Value;
                        break;
                    }

                    if (cancellationSource.IsCancellationRequested)
                    {
                        debuggerStopEventArgs.ResumeAction = DebuggerResumeAction.Stop;
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
                cancellationSource.Dispose();
                cancellationContext.Dispose();
                _debuggingContext.LastResumeAction = null;
                _exitNestedPrompt = false;
                _pwshContext.PopPowerShell();
            }
        }

        private void OnDebuggerResumed(object sender, DebuggerResumedArgs debuggerResumedArgs)
        {
            _debuggingContext.OnDebuggerResume(sender, debuggerResumedArgs);
            ExitNestedPrompt();
            DebuggerResumed?.Invoke(this, debuggerResumedArgs);
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs breakpointUpdatedEventArgs)
        {
            BreakpointUpdated?.Invoke(this, breakpointUpdatedEventArgs);
        }

        private class DebuggingContext
        {
            private CancellationTokenSource _debuggerCancellationTokenSource;

            public CancellationToken? DebuggerResumeCancellationToken => _debuggerCancellationTokenSource?.Token;

            public DebuggerResumeAction? LastResumeAction { get; set; }

            public void OnDebuggerStop(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
            {
                _debuggerCancellationTokenSource = new CancellationTokenSource();
            }

            public void OnDebuggerResume(object sender, DebuggerResumedArgs debuggerResumedArgs)
            {
                LastResumeAction = debuggerResumedArgs.ResumeAction;

                if (_debuggerCancellationTokenSource != null)
                {
                    try
                    {
                        _debuggerCancellationTokenSource.Cancel();
                    }
                    finally
                    {
                        _debuggerCancellationTokenSource.Dispose();
                        _debuggerCancellationTokenSource = null;
                    }
                }
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
    }
}
