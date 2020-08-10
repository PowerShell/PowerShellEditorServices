using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
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
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    internal class PipelineThreadRunner
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

        private readonly IPowerShellContext _pwshContext;

        private readonly BlockingCollection<ISynchronousTask> _executionQueue;

        private readonly CancellationTokenSource _consumerThreadCancellationSource;

        private readonly Thread _pipelineThread; 

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

        private readonly IPowerShellDebugContext _debugContext;

        private readonly ConcurrentStack<CancellationTokenSource> _loopCancellationStack;

        private readonly ConcurrentStack<CancellationTokenSource> _commandCancellationStack;

        private readonly ReaderWriterLockSlim _taskProcessingLock;

        private bool _isExiting;

        private bool _runIdleLoop;

        public PipelineThreadRunner()
        {
            _pipelineThread = new Thread(RunTopLevelConsumerLoop)
            {
                Name = "PSES Execution Service Thread",
            };
            _pipelineThread.SetApartmentState(ApartmentState.STA);
        }

        public Task<TResult> QueueTask<TResult>(SynchronousTask<TResult> synchronousTask)
        {
            _executionQueue.Add(synchronousTask);
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
            if (_commandCancellationStack.TryPeek(out CancellationTokenSource currentCommandCancellation))
            {
                currentCommandCancellation.Cancel();
            }
        }

        public void Dispose()
        {
            Stop();
            _pwshContext.Dispose();
        }


        private void RunTopLevelConsumerLoop()
        {
            Initialize();

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
            var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_debugContext.DebuggerResumeCancellationToken, cancellationContext.CancellationToken);

            try
            {
                DebuggerStopped?.Invoke(this, _debugContext.LastStopEventArgs);

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
                _debugContext.ResetCurrentStopContext();
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

            _consoleRepl.StartRepl();
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

        private void OnPowerShellIdle()
        {
            if (_executionQueue.Count == 0)
            {
                return;
            }

            _runIdleLoop = true;
            PushNonInteractivePowerShell();
        }

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
        {
            IsDebuggerStopped = true;
            CurrentPowerShell.WaitForRemoteOutputIfNeeded();
            _debugContext.OnDebuggerStop(sender, debuggerStopEventArgs);
            PushDebugPowerShell();
            CurrentPowerShell.ResumeRemoteOutputIfNeeded();
            IsDebuggerStopped = false;
        }

        private void SetDebuggerResuming(DebuggerResumeAction resumeAction)
        {
            _consoleRepl.SetReplPop();
            _debugContext.SetDebuggerResuming(resumeAction);
            DebuggerResuming?.Invoke(this, new DebuggerResumingEventArgs(resumeAction));
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs breakpointUpdatedEventArgs)
        {
            BreakpointUpdated?.Invoke(this, breakpointUpdatedEventArgs);
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
            _consoleRepl.SetReplPop();
            CancelCurrentTask();

            RunspaceStateInfo oldRunspaceState = CurrentPowerShell.Runspace.RunspaceStateInfo;
            _taskProcessingLock.EnterWriteLock();
            try
            {
                while (_psFrameStack.Count > 0
                    && !_psFrameStack.Peek().PowerShell.Runspace.RunspaceStateInfo.IsUsable())
                {
                    PopFrame();
                }

                if (_psFrameStack.Count == 0)
                {
                    // If our main runspace was corrupted,
                    // we must re-initialize our state.
                    // TODO: Use runspace.ResetRunspaceState() here instead
                    Initialize();

                    _logger.LogError($"Top level runspace entered state '{oldRunspaceState.State}' for reason '{oldRunspaceState.Reason}' and was reinitialized."
                        + " Please report this issue in the PowerShell/vscode-PowerShell GitHub repository with these logs.");
                    EditorServicesHost.UI.WriteErrorLine("The main runspace encountered an error and has been reinitialized. See the PowerShell extension logs for more details.");
                }
                else
                {
                    _logger.LogError($"Current runspace entered state '{oldRunspaceState.State}' for reason '{oldRunspaceState.Reason}' and was popped.");
                    EditorServicesHost.UI.WriteErrorLine($"The current runspace entered state '{oldRunspaceState.State}' for reason '{oldRunspaceState.Reason}'."
                        + " If this occurred when using Ctrl+C in a Windows PowerShell remoting session, this is expected behavior."
                        + " The session is now returning to the previous runspace.");
                }
            }
            finally
            {
                _taskProcessingLock.ExitWriteLock();
            }
        }

    }
}
