using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
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
            HostStartupInfo hostInfo)
        {
            var executionService = new PowerShellExecutionService(
                loggerFactory,
                hostInfo.Name,
                hostInfo.Version,
                hostInfo.LanguageMode,
                hostInfo.PSHost,
                hostInfo.ProfilePaths,
                hostInfo.AdditionalModules);

            executionService.Start();

            return executionService;
        }

        private readonly CancellationTokenSource _stopThreadCancellationSource;

        private readonly BlockingCollection<ISynchronousTask> _executionQueue;

        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        private readonly string _hostName;

        private readonly Version _hostVersion;

        private readonly PSLanguageMode _languageMode;

        private readonly PSHost _internalHost;

        private readonly ProfilePathInfo _profilePaths;

        private readonly IReadOnlyList<string> _additionalModulesToLoad;

        private Thread _pipelineThread;

        private CancellationTokenSource _currentExecutionCancellationSource;

        private SMA.PowerShell _pwsh;

        private PowerShellConsoleService _consoleService;

        private bool _taskRunning;

        private PowerShellExecutionService(
            ILoggerFactory loggerFactory,
            string hostName,
            Version hostVersion,
            PSLanguageMode languageMode,
            PSHost internalHost,
            ProfilePathInfo profilePaths,
            IReadOnlyList<string> additionalModules)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<PowerShellExecutionService>();
            _stopThreadCancellationSource = new CancellationTokenSource();
            _executionQueue = new BlockingCollection<ISynchronousTask>();
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

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            Func<SMA.PowerShell, CancellationToken, TResult> func,
            string representation,
            CancellationToken cancellationToken)
        {
            TResult appliedFunc(CancellationToken cancellationToken) => func(_pwsh, cancellationToken);
            return ExecuteDelegateAsync(appliedFunc, representation, cancellationToken);
        }

        public Task ExecuteDelegateAsync(
            Action<SMA.PowerShell, CancellationToken> action,
            string representation,
            CancellationToken cancellationToken)
        {
            void appliedAction(CancellationToken cancellationToken) => action(_pwsh, cancellationToken);
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

        public Task<Collection<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken)
        {
            Task<Collection<TResult>> result = QueueTask(new SynchronousPowerShellTask<TResult>(_logger, _pwsh, EditorServicesHost, psCommand, executionOptions, cancellationToken));

            if (executionOptions.InterruptCommandPrompt)
            {
                _consoleService?.CancelCurrentPrompt();
            }

            return result;
        }

        public Task ExecutePSCommandAsync(
            PSCommand psCommand,
            PowerShellExecutionOptions executionOptions,
            CancellationToken cancellationToken) => ExecutePSCommandAsync<PSObject>(psCommand, executionOptions, cancellationToken);

        public void Stop()
        {
            _stopThreadCancellationSource.Cancel();
            _pipelineThread.Join();
        }

        public void CancelCurrentTask()
        {
            if (_taskRunning)
            {
                _currentExecutionCancellationSource?.Cancel();
            }
        }

        public void RegisterConsoleService(PowerShellConsoleService consoleService)
        {
            _consoleService = consoleService;
        }

        public void Dispose()
        {
            Stop();
            _pwsh.Dispose();
        }

        private Task<T> QueueTask<T>(SynchronousTask<T> task)
        {
            _executionQueue.Add(task);
            return task.Task;
        }

        private void Start()
        {
            _pipelineThread = new Thread(RunConsumerLoop)
            {
                Name = "PSES Execution Service Thread"
            };
            _pipelineThread.Start();
        }

        private void Initialize()
        {
            _pwsh = SMA.PowerShell.Create();

            ReadLine = new ConsoleReadLine();

            EditorServicesHost = new EditorServicesConsolePSHost(_loggerFactory, _hostName, _hostVersion, _internalHost, ReadLine);

            _pwsh.Runspace = CreateRunspace(EditorServicesHost, _languageMode);
            Runspace.DefaultRunspace = _pwsh.Runspace;
            EditorServicesHost.RegisterRunspace(_pwsh.Runspace);

            var engineIntrinsics = (EngineIntrinsics)_pwsh.Runspace.SessionStateProxy.GetVariable("ExecutionContext");

            PSReadLineProxy = PSReadLineProxy.LoadAndCreate(_loggerFactory, _pwsh);
            PSReadLineProxy.OverrideIdleHandler(HandlePowerShellOnIdle);
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

        private void RunConsumerLoop()
        {
            Initialize();

            try
            {
                foreach (ISynchronousTask synchronousTask in _executionQueue.GetConsumingEnumerable())
                {
                    RunTaskSynchronously(synchronousTask);
                }
            }
            catch (OperationCanceledException)
            {
                // End nicely
            }
        }

        private void HandlePowerShellOnIdle()
        {
            while (_pwsh.InvocationStateInfo.State == PSInvocationState.Completed
                && _executionQueue.TryTake(out ISynchronousTask task))
            {
                RunTaskSynchronously(task);
            }

            // TODO: Run nested pipeline here for engine event handling
        }

        private void RunTaskSynchronously(ISynchronousTask task)
        {
            if (task.IsCanceled)
            {
                return;
            }

            using (_currentExecutionCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(_stopThreadCancellationSource.Token))
            {
                _taskRunning = true;
                try
                {
                    task.ExecuteSynchronously(_currentExecutionCancellationSource.Token);
                }
                finally
                {
                    _taskRunning = false;
                }
            }
        }

        private void SetExecutionPolicy()
        {
            // We want to get the list hierarchy of execution policies
            // Calling the cmdlet is the simplest way to do that
            IReadOnlyList<PSObject> policies = _pwsh
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
                _pwsh.AddCommand("Microsoft.PowerShell.Security\\Set-ExecutionPolicy")
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

            _pwsh.Runspace.SessionStateProxy.SetVariable("PROFILE", profileVariable);
        }

        private void AddProfileMemberAndLoadIfExists(PSObject profileVariable, string profileName, string profilePath)
        {
            profileVariable.Members.Add(new PSNoteProperty(profileName, profilePath));

            if (File.Exists(profilePath))
            {
                _pwsh.AddScript(profilePath, useLocalScope: false).AddOutputCommand().InvokeAndClear();
            }
        }

        private void ImportModule(string moduleNameOrPath)
        {
            _pwsh.AddCommand("Microsoft.PowerShell.Core\\Import-Module")
                .AddParameter("-Name", moduleNameOrPath)
                .InvokeAndClear();
        }

        private static Runspace CreateRunspace(
            PSHost psHost,
            PSLanguageMode languageMode)
        {
            InitialSessionState iss = Environment.GetEnvironmentVariable("PSES_TEST_USE_CREATE_DEFAULT") == "1"
                ? InitialSessionState.CreateDefault()
                : InitialSessionState.CreateDefault2();

            iss.LanguageMode = languageMode;

            Runspace runspace = RunspaceFactory.CreateRunspace(psHost, iss);

            runspace.SetApartmentStateToSta();
            runspace.ThreadOptions = PSThreadOptions.ReuseThread;

            runspace.Open();

            return runspace;
        }
    }
}
