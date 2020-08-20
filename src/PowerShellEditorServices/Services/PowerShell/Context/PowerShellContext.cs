using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using System.Threading;
using SMA = System.Management.Automation;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Context
{
    using OmniSharp.Extensions.LanguageServer.Protocol.Server;
    using System.Management.Automation.Runspaces;

    internal class PowerShellContext : IPowerShellContext
    {
        private static readonly string s_commandsModulePath = Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "../../Commands/PowerShellEditorServices.Commands.psd1"));

        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        private readonly Stack<PowerShellContextFrame> _psFrameStack;

        private readonly HostStartupInfo _hostInfo;

        private readonly PowerShellExecutionService _executionService;

        private readonly ConsoleReplRunner _consoleReplRunner;

        private readonly PipelineThreadExecutor _pipelineExecutor;

        public PowerShellContext(
            ILoggerFactory loggerFactory,
            HostStartupInfo hostInfo,
            ILanguageServer languageServer,
            PowerShellExecutionService executionService,
            PipelineThreadExecutor pipelineExecutor)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<PowerShellContext>();
            _hostInfo = hostInfo;
            _executionService = executionService;
            _pipelineExecutor = pipelineExecutor;
            _psFrameStack = new Stack<PowerShellContextFrame>();
            DebugContext = new PowerShellDebugContext(languageServer, this, _consoleReplRunner);

            if (hostInfo.ConsoleReplEnabled)
            {
                _consoleReplRunner = new ConsoleReplRunner(_loggerFactory, executionService);
            }
        }

        public SMA.PowerShell CurrentPowerShell => CurrentFrame.PowerShell;

        public Runspace CurrentRunspace => CurrentPowerShell.Runspace;

        public PowerShellContextFrame CurrentFrame => _psFrameStack.Peek();

        public ConsoleReadLine ReadLine { get; private set; }

        public PSReadLineProxy PSReadLineProxy { get; private set; }

        public EditorServicesConsolePSHost EditorServicesHost { get; private set; }

        public EngineIntrinsics EngineIntrinsics { get; private set; }

        public PowerShellDebugContext DebugContext { get; }

        public RunspaceInfo CurrentRunspaceInfo { get; }

        public RunspaceInfo RunspaceContext { get; }

        public CancellationTokenSource CurrentCancellationSource => throw new NotImplementedException();

        public EditorServicesConsolePSHost EditorServicesPSHost => throw new NotImplementedException();

        public bool IsRunspacePushed => throw new NotImplementedException();

        public string InitialWorkingDirectory { get; private set; }

        public void PushInitialPowerShell()
        {
            ReadLine = new ConsoleReadLine();

            EditorServicesHost = new EditorServicesConsolePSHost(
                _loggerFactory,
                _hostInfo.Name,
                _hostInfo.Version,
                _hostInfo.PSHost,
                ReadLine);

            Runspace runspace = CreateInitialRunspace();

            var pwsh = SMA.PowerShell.Create();
            pwsh.Runspace = runspace;
            _psFrameStack.Push(new PowerShellContextFrame(pwsh, PowerShellFrameType.Normal, new CancellationTokenSource()));

            EditorServicesHost.RegisterPowerShellContext(this);

            EngineIntrinsics = (EngineIntrinsics)CurrentFrame.PowerShell.Runspace.SessionStateProxy.GetVariable("ExecutionContext");

            PSReadLineProxy = PSReadLineProxy.LoadAndCreate(_loggerFactory, CurrentPowerShell);
            PSReadLineProxy.OverrideIdleHandler(_pipelineExecutor.OnPowerShellIdle);
            ReadLine.RegisterExecutionDependencies(_executionService, EngineIntrinsics, PSReadLineProxy);

            if (VersionUtils.IsWindows)
            {
                SetExecutionPolicy();
            }

            LoadProfiles();

            ImportModule(s_commandsModulePath);

            if (_hostInfo.AdditionalModules != null && _hostInfo.AdditionalModules.Count > 0)
            {
                foreach (string module in _hostInfo.AdditionalModules)
                {
                    ImportModule(module);
                }
            }

            _consoleReplRunner?.StartRepl();
        }

        public void SetShouldExit(int? exitCode)
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

        public void ProcessDebuggerResult(DebuggerCommandResults debuggerResult)
        {
            if (debuggerResult.ResumeAction != null)
            {
                DebugContext.RaiseDebuggerResumingEvent(new DebuggerResumingEventArgs(debuggerResult.ResumeAction.Value));
            }
        }

        public void PushNestedPowerShell()
        {
            PushNestedPowerShell(PowerShellFrameType.Normal);
        }

        public void PushPowerShell(Runspace runspaceToUse)
        {
            var pwsh = SMA.PowerShell.Create();
            pwsh.Runspace = runspaceToUse;

            PowerShellFrameType frameType = PowerShellFrameType.Normal;

            if (runspaceToUse.RunspaceIsRemote)
            {
                frameType |= PowerShellFrameType.Remote;
            }

            PushFrame(new PowerShellContextFrame(pwsh, frameType, new CancellationTokenSource()));
        }

        public void PopFrame()
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

        public void Dispose()
        {
            _consoleReplRunner?.Dispose();
            _pipelineExecutor.Dispose();
        }

        private void PushFrame(PowerShellContextFrame frame)
        {
            if (_psFrameStack.Count > 0)
            {
                RemoveRunspaceEventHandlers(CurrentFrame.PowerShell.Runspace);
            }
            AddRunspaceEventHandlers(frame.PowerShell.Runspace);
            _psFrameStack.Push(frame);
            _pipelineExecutor.RunPowerShellLoop(frame.FrameType);
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

        public void PushNonInteractivePowerShell()
        {
            PushNestedPowerShell(PowerShellFrameType.NonInteractive);
        }

        private void PushDebugPowerShell()
        {
            PushNestedPowerShell(PowerShellFrameType.Debug);
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

        #region Initial Runspace Setup

        private Runspace CreateInitialRunspace()
        {
            InitialSessionState iss = Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1"
                ? InitialSessionState.CreateDefault()
                : InitialSessionState.CreateDefault2();

            iss.LanguageMode = _hostInfo.LanguageMode;

            Runspace runspace = RunspaceFactory.CreateRunspace(EditorServicesHost, iss);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;

            runspace.Open();

            AddRunspaceEventHandlers(runspace);

            Runspace.DefaultRunspace = runspace;

            return runspace;
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

            ProfilePathInfo profilePaths = _hostInfo.ProfilePaths;

            AddProfileMemberAndLoadIfExists(profileVariable, nameof(profilePaths.AllUsersAllHosts), profilePaths.AllUsersAllHosts);
            AddProfileMemberAndLoadIfExists(profileVariable, nameof(profilePaths.AllUsersCurrentHost), profilePaths.AllUsersCurrentHost);
            AddProfileMemberAndLoadIfExists(profileVariable, nameof(profilePaths.CurrentUserAllHosts), profilePaths.CurrentUserAllHosts);
            AddProfileMemberAndLoadIfExists(profileVariable, nameof(profilePaths.CurrentUserCurrentHost), profilePaths.CurrentUserCurrentHost);

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

        #endregion /* Initial Runspace Setup */

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
                    PopFrame();
                }

                if (_psFrameStack.Count == 0)
                {
                    // If our main runspace was corrupted,
                    // we must re-initialize our state.
                    // TODO: Use runspace.ResetRunspaceState() here instead
                    PushInitialPowerShell();

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
        }

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
        {
            DebugContext.SetDebuggerStopped(debuggerStopEventArgs);
            try
            {
                CurrentPowerShell.WaitForRemoteOutputIfNeeded();
                DebugContext.LastStopEventArgs = debuggerStopEventArgs;
                PushDebugPowerShell();
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

    }
}
