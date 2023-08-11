// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation.Host;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Host
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    // NOTE: These last three are for a workaround for temporary Extension Terminals.
    using Microsoft.PowerShell.EditorServices.Handlers;
    using Microsoft.PowerShell.EditorServices.Server;
    using OmniSharp.Extensions.DebugAdapter.Protocol.Server;

#pragma warning disable CA1506 // Coupling complexity we don't care about
    internal class PsesInternalHost : PSHost, IHostSupportsInteractiveSession, IRunspaceContext, IInternalPowerShellExecutionService
#pragma warning restore CA1506
    {
        internal const string DefaultPrompt = "> ";

        private static readonly PSCommand s_promptCommand = new PSCommand().AddCommand("prompt");

        private static readonly PropertyInfo s_scriptDebuggerTriggerObjectProperty;

        /// <summary>
        /// To workaround a horrid bug where the `TranscribeOnly` field of the PSHostUserInterface
        /// can accidentally remain true, we have to use a bunch of reflection so that <see
        /// cref="DisableTranscribeOnly()" /> can reset it to false. (This was fixed in PowerShell
        /// 7.) Note that it must be the internal UI instance, not our own UI instance, otherwise
        /// this would be easier. Because of the amount of reflection involved, we contain it to
        /// only PowerShell 5.1 at compile-time, and we have to set this up in this class, not <see
        /// cref="SynchronousPowerShellTask" /> because that's templated, making statics practically
        /// useless. <see cref = "SynchronousPowerShellTask.ExecuteNormally" /> method calls <see
        /// cref="DisableTranscribeOnly()" /> when necessary.
        /// See: https://github.com/PowerShell/PowerShell/pull/3436
        /// </summary>
        [ThreadStatic] // Because we can re-use it, but only once per instance of PSES.
        private static PSHostUserInterface s_internalPSHostUserInterface;

        private static readonly Func<PSHostUserInterface, bool> s_getTranscribeOnlyDelegate;

        private static readonly Action<PSHostUserInterface, bool> s_setTranscribeOnlyDelegate;

        private static readonly PropertyInfo s_executionContextProperty;

        private static readonly PropertyInfo s_internalHostProperty;

        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        private readonly ILanguageServerFacade _languageServer;

        /// <summary>
        /// TODO: Improve this coupling. It's assigned by <see cref="PsesDebugServer.StartAsync()" />
        /// so that the PowerShell process started when <see cref="PsesLaunchRequestArguments.CreateTemporaryIntegratedConsole" />
        /// is true can also receive the required 'sendKeyPress' notification to return from a
        /// canceled <see cref="System.Console.ReadKey()" />.
        /// </summary>
        internal IDebugAdapterServerFacade DebugServer;

        private readonly HostStartupInfo _hostInfo;

        private readonly BlockingConcurrentDeque<ISynchronousTask> _taskQueue;

        private readonly Stack<PowerShellContextFrame> _psFrameStack;

        private readonly Stack<RunspaceFrame> _runspaceStack;

        private readonly CancellationContext _cancellationContext;

        internal readonly ReadLineProvider _readLineProvider;

        private readonly Thread _pipelineThread;

        private readonly IdempotentLatch _isRunningLatch = new();

        private readonly TaskCompletionSource<bool> _started = new();

        private readonly TaskCompletionSource<bool> _stopped = new();

        private EngineIntrinsics _mainRunspaceEngineIntrinsics;

        private bool _shouldExit;

        private int _shuttingDown;

        private string _localComputerName;

        private bool _shellIntegrationEnabled;

        private ConsoleKeyInfo? _lastKey;

        private bool _skipNextPrompt;

        private CancellationToken _readKeyCancellationToken;

        private bool _resettingRunspace;

        static PsesInternalHost()
        {
            Type scriptDebuggerType = typeof(PSObject).Assembly
                .GetType("System.Management.Automation.ScriptDebugger");

            if (scriptDebuggerType is null)
            {
                return;
            }

            s_scriptDebuggerTriggerObjectProperty = scriptDebuggerType.GetProperty(
                "TriggerObject",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (VersionUtils.IsNetCore)
            {
                // The following reflection methods are only needed for the .NET Framework.
                return;
            }

            PropertyInfo transcribeOnlyProperty = typeof(PSHostUserInterface)
                .GetProperty("TranscribeOnly", BindingFlags.NonPublic | BindingFlags.Instance);

            MethodInfo transcribeOnlyGetMethod = transcribeOnlyProperty.GetGetMethod(nonPublic: true);

            s_getTranscribeOnlyDelegate = (Func<PSHostUserInterface, bool>)Delegate.CreateDelegate(
                typeof(Func<PSHostUserInterface, bool>), transcribeOnlyGetMethod);

            MethodInfo transcribeOnlySetMethod = transcribeOnlyProperty.GetSetMethod(nonPublic: true);

            s_setTranscribeOnlyDelegate = (Action<PSHostUserInterface, bool>)Delegate.CreateDelegate(
                typeof(Action<PSHostUserInterface, bool>), transcribeOnlySetMethod);

            s_executionContextProperty = typeof(Runspace)
                .GetProperty("ExecutionContext", BindingFlags.NonPublic | BindingFlags.Instance);

            s_internalHostProperty = s_executionContextProperty.PropertyType
                .GetProperty("InternalHost", BindingFlags.NonPublic | BindingFlags.Instance);
        }

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

            // Default stack size on .NET Framework is 524288 (512KB) (as reported by GetProcessDefaultStackSize)
            // this leaves very little room in the stack. Windows PowerShell internally sets the value based on
            // PipelineMaxStackSizeMB as seen here: https://github.com/PowerShell/PowerShell/issues/1187,
            // which has default of 10 and multiplies that by 1_000_000, so the default stack size is
            // 10_000_000 (~10MB) when starting in normal console host.
            //
            // For PS7 the value is ignored by .NET because settings the stack size is not supported, but we can
            // still provide 0, which means fallback to the default in both .NET and .NET Framework.
            int maxStackSize = VersionUtils.IsPS5 ? 10_000_000 : 0;
            _pipelineThread = new Thread(Run, maxStackSize)
            {
                Name = "PSES Pipeline Execution Thread",
            };

            if (VersionUtils.IsWindows)
            {
                _pipelineThread.SetApartmentState(ApartmentState.STA);
            }

            PublicHost = new EditorServicesConsolePSHost(this);
            Name = hostInfo.Name;
            Version = hostInfo.Version;

            DebugContext = new PowerShellDebugContext(loggerFactory, this);
            UI = hostInfo.ConsoleReplEnabled
                ? new EditorServicesConsolePSHostUserInterface(loggerFactory, hostInfo.PSHost.UI)
                : new NullPSHostUI();
        }

        public override CultureInfo CurrentCulture => _hostInfo.PSHost.CurrentCulture;

        public override CultureInfo CurrentUICulture => _hostInfo.PSHost.CurrentUICulture;

        public override Guid InstanceId { get; } = Guid.NewGuid();

        public override string Name { get; }

        public override PSObject PrivateData => _hostInfo.PSHost.PrivateData;

        public override PSHostUserInterface UI { get; }

        public override Version Version { get; }

        public bool IsRunspacePushed { get; private set; }

        public Runspace Runspace => _runspaceStack.Peek().Runspace;

        public RunspaceInfo CurrentRunspace => CurrentFrame.RunspaceInfo;

        public PowerShell CurrentPowerShell => CurrentFrame.PowerShell;

        public EditorServicesConsolePSHost PublicHost { get; }

        public PowerShellDebugContext DebugContext { get; }

        public bool IsRunning => _isRunningLatch.IsSignaled;

        public Task Shutdown => _stopped.Task;

        IRunspaceInfo IRunspaceContext.CurrentRunspace => CurrentRunspace;

        internal PowerShellContextFrame CurrentFrame => _psFrameStack.Peek();

        public event Action<object, RunspaceChangedEventArgs> RunspaceChanged;

        private bool ShouldExitExecutionLoop => _shouldExit || _shuttingDown != 0;

        public override void EnterNestedPrompt() => PushPowerShellAndRunLoop(
            CreateNestedPowerShell(CurrentRunspace),
            PowerShellFrameType.Nested | PowerShellFrameType.Repl);

        public override void ExitNestedPrompt() => SetExit();

        public override void NotifyBeginApplication() => _hostInfo.PSHost.NotifyBeginApplication();

        public override void NotifyEndApplication() => _hostInfo.PSHost.NotifyEndApplication();

        public void PopRunspace()
        {
            if (!Runspace.RunspaceIsRemote)
            {
                return;
            }

            IsRunspacePushed = false;
            CurrentFrame.SessionExiting = true;
            PopPowerShell();
            SetExit();
        }

        public void PushRunspace(Runspace runspace)
        {
            IsRunspacePushed = true;
            PushPowerShellAndMaybeRunLoop(
                CreatePowerShellForRunspace(runspace),
                PowerShellFrameType.Remote | PowerShellFrameType.Repl,
                skipRunLoop: true);
        }

        // TODO: Handle exit code if needed
        public override void SetShouldExit(int exitCode)
        {
            if (CurrentFrame.IsRemote)
            {
                // PopRunspace also calls SetExit.
                PopRunspace();
                return;
            }

            SetExit();
        }

        /// <summary>
        /// Try to start the PowerShell loop in the host.
        /// If the host is already started, this is idempotent.
        /// Returns when the host is in a valid initialized state.
        /// </summary>
        /// <param name="startOptions">Options to configure host startup.</param>
        /// <param name="cancellationToken">A token to cancel startup.</param>
        /// <returns>A task that resolves when the host has finished startup, with the value true if the caller started the host, and false otherwise.</returns>
        public async Task<bool> TryStartAsync(HostStartOptions startOptions, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Starting host...");
            if (!_isRunningLatch.TryEnter())
            {
                _logger.LogDebug("Host start requested after already started.");
                await _started.Task.ConfigureAwait(false);
                return false;
            }

            _pipelineThread.Start();

            if (startOptions.LoadProfiles)
            {
                _logger.LogDebug("Loading profiles...");
                await LoadHostProfilesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Profiles loaded!");
            }

            if (startOptions.ShellIntegrationEnabled)
            {
                _logger.LogDebug("Enabling shell integration...");
                _shellIntegrationEnabled = true;
                await EnableShellIntegrationAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Shell integration enabled!");
            }
            else
            {
                _logger.LogDebug("Shell integration not enabled!");
            }

            if (startOptions.InitialWorkingDirectory is not null)
            {
                _logger.LogDebug($"Setting InitialWorkingDirectory to {startOptions.InitialWorkingDirectory}...");
                await SetInitialWorkingDirectoryAsync(startOptions.InitialWorkingDirectory, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("InitialWorkingDirectory set!");
            }

            await _started.Task.ConfigureAwait(false);
            return true;
        }

        public Task StopAsync()
        {
            TriggerShutdown();
            return Shutdown;
        }

        public void TriggerShutdown()
        {
            _logger.LogDebug("Shutting down host...");
            if (Interlocked.Exchange(ref _shuttingDown, 1) == 0)
            {
                _cancellationContext.CancelCurrentTaskStack();
            }
        }

        public void SetExit()
        {
            // Can't exit from the top level of PSES
            // since if you do, you lose all LSP services
            PowerShellContextFrame frame = CurrentFrame;
            if (!frame.IsRepl || _psFrameStack.Count <= 1)
            {
                return;
            }

            _shouldExit = true;
        }

        internal void ForceSetExit() => _shouldExit = true;

        private void SetBusy(bool busy) => _languageServer?.SendNotification("powerShell/executionBusyStatus", busy);

        private bool CancelForegroundAndPrepend(ISynchronousTask task, bool isIdle = false)
        {
            // NOTE: This causes foreground tasks to act like they have `ExecutionPriority.Next`.
            //
            // When a task must displace the current foreground command,
            // we must:
            //  - block the consumer thread from mutating the queue
            //  - cancel any running task on the consumer thread
            //  - place our task on the front of the queue
            //  - skip the next prompt so the task runs instead
            //  - unblock the consumer thread
            if (!task.ExecutionOptions.RequiresForeground)
            {
                return false;
            }

            _skipNextPrompt = true;

            if (task is ISynchronousPowerShellTask t)
            {
                t.MaybeAddToHistory();
            }

            using (_taskQueue.BlockConsumers())
            {
                _taskQueue.Prepend(task);
                if (isIdle)
                {
                    CancelIdleParentTask();
                }
                else
                {
                    CancelCurrentTask();
                }
            }

            return true;
        }

        // This handles executing the task while also notifying the client that the pipeline is
        // currently busy with a PowerShell task. The extension indicates this with a spinner.
        private void ExecuteTaskSynchronously(ISynchronousTask task, CancellationToken cancellationToken)
        {
            // TODO: Simplify this logic.
            bool busy = false;
            if (task is ISynchronousPowerShellTask t
                && (t.PowerShellExecutionOptions.AddToHistory
                    || t.PowerShellExecutionOptions.FromRepl))
            {
                busy = true;
                SetBusy(true);
            }
            try
            {
                task.ExecuteSynchronously(cancellationToken);
            }
            finally
            {
                if (busy)
                {
                    SetBusy(false);
                }
            }
        }

        public Task<T> InvokeTaskOnPipelineThreadAsync<T>(SynchronousTask<T> task)
        {
            if (CancelForegroundAndPrepend(task))
            {
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

        public void CancelCurrentTask() => _cancellationContext.CancelCurrentTask();

        public void CancelIdleParentTask() => _cancellationContext.CancelIdleParentTask();

        public void UnwindCallStack() => _cancellationContext.CancelCurrentTaskStack();

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            Func<PowerShell, CancellationToken, TResult> func,
            CancellationToken cancellationToken)
        {
            return InvokeTaskOnPipelineThreadAsync(
                new SynchronousPSDelegateTask<TResult>(_logger, this, representation, executionOptions, func, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            Action<PowerShell, CancellationToken> action,
            CancellationToken cancellationToken)
        {
            return InvokeTaskOnPipelineThreadAsync(
                new SynchronousPSDelegateTask(_logger, this, representation, executionOptions, action, cancellationToken));
        }

        public Task<TResult> ExecuteDelegateAsync<TResult>(
            string representation,
            ExecutionOptions executionOptions,
            Func<CancellationToken, TResult> func,
            CancellationToken cancellationToken)
        {
            return InvokeTaskOnPipelineThreadAsync(
                new SynchronousDelegateTask<TResult>(_logger, representation, executionOptions, func, cancellationToken));
        }

        public Task ExecuteDelegateAsync(
            string representation,
            ExecutionOptions executionOptions,
            Action<CancellationToken> action,
            CancellationToken cancellationToken)
        {
            return InvokeTaskOnPipelineThreadAsync(
                new SynchronousDelegateTask(_logger, representation, executionOptions, action, cancellationToken));
        }

        public Task<IReadOnlyList<TResult>> ExecutePSCommandAsync<TResult>(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null)
        {
            return InvokeTaskOnPipelineThreadAsync(
                new SynchronousPowerShellTask<TResult>(_logger, this, psCommand, executionOptions, cancellationToken));
        }

        public Task ExecutePSCommandAsync(
            PSCommand psCommand,
            CancellationToken cancellationToken,
            PowerShellExecutionOptions executionOptions = null) => ExecutePSCommandAsync<PSObject>(psCommand, cancellationToken, executionOptions);

        public TResult InvokeDelegate<TResult>(string representation, ExecutionOptions executionOptions, Func<CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            SynchronousDelegateTask<TResult> task = new(_logger, representation, executionOptions, func, cancellationToken);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokeDelegate(string representation, ExecutionOptions executionOptions, Action<CancellationToken> action, CancellationToken cancellationToken)
        {
            SynchronousDelegateTask task = new(_logger, representation, executionOptions, action, cancellationToken);
            task.ExecuteAndGetResult(cancellationToken);
        }

        public IReadOnlyList<TResult> InvokePSCommand<TResult>(PSCommand psCommand, PowerShellExecutionOptions executionOptions, CancellationToken cancellationToken)
        {
            SynchronousPowerShellTask<TResult> task = new(_logger, this, psCommand, executionOptions, cancellationToken);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokePSCommand(PSCommand psCommand, PowerShellExecutionOptions executionOptions, CancellationToken cancellationToken) => InvokePSCommand<PSObject>(psCommand, executionOptions, cancellationToken);

        public TResult InvokePSDelegate<TResult>(string representation, ExecutionOptions executionOptions, Func<PowerShell, CancellationToken, TResult> func, CancellationToken cancellationToken)
        {
            SynchronousPSDelegateTask<TResult> task = new(_logger, this, representation, executionOptions, func, cancellationToken);
            return task.ExecuteAndGetResult(cancellationToken);
        }

        public void InvokePSDelegate(string representation, ExecutionOptions executionOptions, Action<PowerShell, CancellationToken> action, CancellationToken cancellationToken)
        {
            SynchronousPSDelegateTask task = new(_logger, this, representation, executionOptions, action, cancellationToken);
            task.ExecuteAndGetResult(cancellationToken);
        }

        internal void AddToHistory(string historyEntry) => _readLineProvider.ReadLine.AddToHistory(historyEntry);

        // This works around a bug in PowerShell 5.1 (that was later fixed) where a running
        // transcription could cause output to disappear since the `TranscribeOnly` property was
        // accidentally not reset to false.
        internal void DisableTranscribeOnly()
        {
            if (VersionUtils.IsNetCore)
            {
                return;
            }

            // To fix the TranscribeOnly bug, we have to get the internal UI, which involves a lot
            // of reflection since we can't always just use PowerShell to execute `$Host.UI`.
            s_internalPSHostUserInterface ??=
                (s_internalHostProperty.GetValue(
                    s_executionContextProperty.GetValue(CurrentPowerShell.Runspace))
                    as PSHost)?.UI;

            if (s_internalPSHostUserInterface is null)
            {
                return;
            }

            if (s_getTranscribeOnlyDelegate(s_internalPSHostUserInterface))
            {
                s_setTranscribeOnlyDelegate(s_internalPSHostUserInterface, false);
            }
        }

        internal Task LoadHostProfilesAsync(CancellationToken cancellationToken)
        {
            // NOTE: This is a special task run on startup!
            return ExecuteDelegateAsync(
                "LoadProfiles",
                executionOptions: null,
                (pwsh, _) => pwsh.LoadProfiles(_hostInfo.ProfilePaths),
                cancellationToken);
        }

        private Task EnableShellIntegrationAsync(CancellationToken cancellationToken)
        {
            // Imported on 01/03/23 from
            // https://github.com/microsoft/vscode/blob/main/src/vs/workbench/contrib/terminal/browser/media/shellIntegration.ps1
            // with quotes escaped, `__VSCodeOriginalPSConsoleHostReadLine` removed (as it's done
            // in our own ReadLine function), and `[Console]::Write` replaced with `Write-Host`.
            const string shellIntegrationScript = @"
# Prevent installing more than once per session
if (Test-Path variable:global:__VSCodeOriginalPrompt) {
	return;
}

# Disable shell integration when the language mode is restricted
if ($ExecutionContext.SessionState.LanguageMode -ne ""FullLanguage"") {
	return;
}

$Global:__VSCodeOriginalPrompt = $function:Prompt

$Global:__LastHistoryId = -1

function Global:__VSCode-Escape-Value([string]$value) {
	# NOTE: In PowerShell v6.1+, this can be written `$value -replace '…', { … }` instead of `[regex]::Replace`.
	# Replace any non-alphanumeric characters.
	[regex]::Replace($value, '[\\\n;]', { param($match)
		# Encode the (ascii) matches as `\x<hex>`
		-Join (
			[System.Text.Encoding]::UTF8.GetBytes($match.Value) | ForEach-Object { '\x{0:x2}' -f $_ }
		)
	})
}

function Global:Prompt() {
	# NOTE: We disable strict mode for the scope of this function because it unhelpfully throws an
	# error when $LastHistoryEntry is null, and is not otherwise useful.
	Set-StrictMode -Off
	$FakeCode = [int]!$global:?
	$LastHistoryEntry = Get-History -Count 1
	# Skip finishing the command if the first command has not yet started
	if ($Global:__LastHistoryId -ne -1) {
		if ($LastHistoryEntry.Id -eq $Global:__LastHistoryId) {
			# Don't provide a command line or exit code if there was no history entry (eg. ctrl+c, enter on no command)
			$Result  = ""$([char]0x1b)]633;E`a""
			$Result += ""$([char]0x1b)]633;D`a""
		} else {
			# Command finished command line
			# OSC 633 ; A ; <CommandLine?> ST
			$Result  = ""$([char]0x1b)]633;E;""
			# Sanitize the command line to ensure it can get transferred to the terminal and can be parsed
			# correctly. This isn't entirely safe but good for most cases, it's important for the Pt parameter
			# to only be composed of _printable_ characters as per the spec.
			if ($LastHistoryEntry.CommandLine) {
				$CommandLine = $LastHistoryEntry.CommandLine
			} else {
				$CommandLine = """"
			}
			$Result += $(__VSCode-Escape-Value $CommandLine)
			$Result += ""`a""
			# Command finished exit code
			# OSC 633 ; D [; <ExitCode>] ST
			$Result += ""$([char]0x1b)]633;D;$FakeCode`a""
		}
	}
	# Prompt started
	# OSC 633 ; A ST
	$Result += ""$([char]0x1b)]633;A`a""
	# Current working directory
	# OSC 633 ; <Property>=<Value> ST
	$Result += if($pwd.Provider.Name -eq 'FileSystem'){""$([char]0x1b)]633;P;Cwd=$(__VSCode-Escape-Value $pwd.ProviderPath)`a""}
	# Before running the original prompt, put $? back to what it was:
	if ($FakeCode -ne 0) {
		Write-Error ""failure"" -ea ignore
	}
	# Run the original prompt
	$Result += $Global:__VSCodeOriginalPrompt.Invoke()
	# Write command started
	$Result += ""$([char]0x1b)]633;B`a""
	$Global:__LastHistoryId = $LastHistoryEntry.Id
	return $Result
}

# Set IsWindows property
if ($PSVersionTable.PSVersion -lt ""6.0"") {
	[Console]::Write(""$([char]0x1b)]633;P;IsWindows=$true`a"")
} else {
	[Console]::Write(""$([char]0x1b)]633;P;IsWindows=$IsWindows`a"")
}

# Set always on key handlers which map to default VS Code keybindings
function Set-MappedKeyHandler {
	param ([string[]] $Chord, [string[]]$Sequence)
	$Handler = $(Get-PSReadLineKeyHandler -Chord $Chord | Select-Object -First 1)
	if ($Handler) {
		Set-PSReadLineKeyHandler -Chord $Sequence -Function $Handler.Function
	}
}

function Set-MappedKeyHandlers {
	Set-MappedKeyHandler -Chord Ctrl+Spacebar -Sequence 'F12,a'
	Set-MappedKeyHandler -Chord Alt+Spacebar -Sequence 'F12,b'
	Set-MappedKeyHandler -Chord Shift+Enter -Sequence 'F12,c'
	Set-MappedKeyHandler -Chord Shift+End -Sequence 'F12,d'
}

Set-MappedKeyHandlers
            ";

            return ExecutePSCommandAsync(new PSCommand().AddScript(shellIntegrationScript), cancellationToken);
        }

        public Task SetInitialWorkingDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            return Directory.Exists(path)
                ? ExecutePSCommandAsync(
                    new PSCommand().AddCommand("Set-Location").AddParameter("LiteralPath", path),
                    cancellationToken)
                : Task.CompletedTask;
        }

        private void Run()
        {
            try
            {
                (PowerShell pwsh, RunspaceInfo localRunspaceInfo, EngineIntrinsics engineIntrinsics) = CreateInitialPowerShellSession();
                _mainRunspaceEngineIntrinsics = engineIntrinsics;
                _localComputerName = localRunspaceInfo.SessionDetails.ComputerName;
                _runspaceStack.Push(new RunspaceFrame(pwsh.Runspace, localRunspaceInfo));
                PushPowerShellAndRunLoop(pwsh, PowerShellFrameType.Normal | PowerShellFrameType.Repl, localRunspaceInfo);
            }
            catch (Exception e)
            {
                _started.TrySetException(e);
                _stopped.TrySetException(e);
            }
        }

        private (PowerShell, RunspaceInfo, EngineIntrinsics) CreateInitialPowerShellSession()
        {
            (PowerShell pwsh, EngineIntrinsics engineIntrinsics) = CreateInitialPowerShell(_hostInfo, _readLineProvider);
            RunspaceInfo localRunspaceInfo = RunspaceInfo.CreateFromLocalPowerShell(_logger, pwsh);
            return (pwsh, localRunspaceInfo, engineIntrinsics);
        }

        internal PowerShellContextFrame PushPowerShellForExecution()
        {
            PowerShellContextFrame frame = CurrentFrame;
            PowerShellFrameType currentFrameType = frame.FrameType;
            currentFrameType &= ~PowerShellFrameType.Repl;
            PowerShellContextFrame newFrame = new(
                frame.PowerShell.CloneForNewFrame(),
                frame.RunspaceInfo,
                currentFrameType);

            PushPowerShell(newFrame);
            return newFrame;
        }

        private void PushPowerShellAndRunLoop(PowerShell pwsh, PowerShellFrameType frameType, RunspaceInfo newRunspaceInfo = null)
            => PushPowerShellAndMaybeRunLoop(pwsh, frameType, newRunspaceInfo, skipRunLoop: false);

        private void PushPowerShellAndMaybeRunLoop(
            PowerShell pwsh,
            PowerShellFrameType frameType,
            RunspaceInfo newRunspaceInfo = null,
            bool skipRunLoop = false)
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

            PushPowerShellAndMaybeRunLoop(new PowerShellContextFrame(pwsh, newRunspaceInfo, frameType), skipRunLoop);
        }

        private RunspaceInfo GetRunspaceInfoForPowerShell(PowerShell pwsh, out bool isNewRunspace, out RunspaceFrame oldRunspaceFrame)
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

        private void PushPowerShellAndMaybeRunLoop(PowerShellContextFrame frame, bool skipRunLoop = false)
        {
            PushPowerShell(frame);
            if (skipRunLoop)
            {
                return;
            }

            try
            {
                if (_psFrameStack.Count == 1)
                {
                    RunTopLevelExecutionLoop();
                }
                else if (frame.IsDebug)
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
                if (CurrentFrame != frame)
                {
                    frame.IsAwaitingPop = true;
                }
                else
                {
                    PopPowerShell();
                }
            }
        }

        private void PushPowerShell(PowerShellContextFrame frame)
        {
            if (_psFrameStack.Count > 0)
            {
                if (frame.PowerShell.Runspace == CurrentFrame.PowerShell.Runspace)
                {
                    _psFrameStack.Push(frame);
                    return;
                }

                RemoveRunspaceEventHandlers(CurrentFrame.PowerShell.Runspace);
            }

            AddRunspaceEventHandlers(frame.PowerShell.Runspace);

            _psFrameStack.Push(frame);
        }

        internal void PopPowerShellForExecution(PowerShellContextFrame expectedFrame)
        {
            if (CurrentFrame != expectedFrame)
            {
                expectedFrame.IsAwaitingPop = true;
                return;
            }

            PopPowerShellImpl();
        }

        private void PopPowerShell(RunspaceChangeAction runspaceChangeAction = RunspaceChangeAction.Exit)
        {
            _shouldExit = false;
            PopPowerShellImpl(_ =>
            {
                // If we're changing runspace, make sure we move the handlers over. If we just
                // popped the last frame, then we're exiting and should pop the runspace too.
                if (_psFrameStack.Count == 0 || Runspace != CurrentPowerShell.Runspace)
                {
                    RunspaceFrame previousRunspaceFrame = _runspaceStack.Pop();
                    RemoveRunspaceEventHandlers(previousRunspaceFrame.Runspace);

                    // If there is still a runspace on the stack, then we need to re-register the
                    // handlers. Otherwise we're exiting and so don't need to run 'RunspaceChanged'.
                    if (_runspaceStack.Count > 0)
                    {
                        RunspaceFrame newRunspaceFrame = _runspaceStack.Peek();
                        AddRunspaceEventHandlers(newRunspaceFrame.Runspace);
                        RunspaceChanged?.Invoke(
                            this,
                            new RunspaceChangedEventArgs(
                                runspaceChangeAction,
                                previousRunspaceFrame.RunspaceInfo,
                                newRunspaceFrame.RunspaceInfo));
                    }
                }
            });
        }

        private void PopPowerShellImpl(Action<PowerShellContextFrame> action = null)
        {
            do
            {
                PowerShellContextFrame frame = _psFrameStack.Pop();
                try
                {
                    action?.Invoke(frame);
                }
                finally
                {
                    frame.Dispose();
                }
            }
            while (_psFrameStack.Count > 0 && CurrentFrame.IsAwaitingPop);
        }

        private void RunTopLevelExecutionLoop()
        {
            try
            {
                // Make sure we execute any startup tasks first. These should be, in order:
                // 1. Delegate to register psEditor variable
                // 2. LoadProfiles delegate
                // 3. Delegate to import PSEditModule
                while (_taskQueue.TryTake(out ISynchronousTask task))
                {
                    task.ExecuteSynchronously(CancellationToken.None);
                }

                // Signal that we are ready for outside services to use
                _started.TrySetResult(true);

                // While loop is purely so we can recover gracefully from a
                // terminate exception.
                while (true)
                {
                    try
                    {
                        RunExecutionLoop();
                        break;
                    }
                    catch (TerminateException)
                    {
                        // Do nothing, since we are at the top level of the loop
                        // the call stack has been unwound successfully.
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "PSES pipeline thread loop experienced an unexpected top-level exception");
                _stopped.TrySetException(e);
                return;
            }

            _logger.LogDebug("PSES pipeline thread loop shutting down");
            _stopped.SetResult(true);
        }

        private void RunDebugExecutionLoop()
        {
            try
            {
                DebugContext.EnterDebugLoop();
                RunExecutionLoop(isForDebug: true);
            }
            finally
            {
                DebugContext.ExitDebugLoop();
            }
        }

        private void RunExecutionLoop(bool isForDebug = false)
        {
            Runspace initialRunspace = Runspace;
            while (!ShouldExitExecutionLoop)
            {
                if (isForDebug && !initialRunspace.RunspaceStateInfo.IsUsable())
                {
                    return;
                }

                using CancellationScope cancellationScope = _cancellationContext.EnterScope(false);

                try
                {
                    DoOneRepl(cancellationScope.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (isForDebug)
                    {
                        while (Runspace is { RunspaceIsRemote: true } remoteRunspace
                            && !remoteRunspace.RunspaceStateInfo.IsUsable())
                        {
                            PopPowerShell(RunspaceChangeAction.Exit);
                        }

                        if (ShouldExitExecutionLoop)
                        {
                            return;
                        }
                    }
                }

                while (!ShouldExitExecutionLoop
                    && !cancellationScope.CancellationToken.IsCancellationRequested
                    && _taskQueue.TryTake(out ISynchronousTask task))
                {
                    try
                    {
                        ExecuteTaskSynchronously(task, cancellationScope.CancellationToken);
                    }
                    // Our flaky extension command test seems to be such because sometimes another
                    // task gets queued, and since it runs in the foreground it cancels that task.
                    // Interactively, this happens in the first loop (with DoOneRepl) which catches
                    // the cancellation exception, but when under test that is a no-op, so  it
                    // happens in this second loop. Hence we need to catch it here too.
                    catch (OperationCanceledException e)
                    {
                        _logger.LogDebug(e, "Task {Task} was canceled!", task);
                    }
                }

                if (_shouldExit
                    && CurrentFrame is { IsRemote: true, IsRepl: true, IsNested: false })
                {
                    _shouldExit = false;
                    PopPowerShell();
                }
            }
        }

        private void DoOneRepl(CancellationToken cancellationToken)
        {
            if (!_hostInfo.ConsoleReplEnabled)
            {
                // Throttle the REPL loop with a sleep because we're not interactively reading input from the user.
                Thread.Sleep(100);
                return;
            }

            // TODO: We must remove this awful logic, it causes so much pain. The StopDebugContext()
            // requires that we're not in a prompt that we're skipping, otherwise the debugger is
            // "active" but we haven't yet hit a breakpoint.
            //
            // When a task must run in the foreground, we cancel out of the idle loop and return to
            // the top level. At that point, we would normally run a REPL, but we need to
            // immediately execute the task. So we set _skipNextPrompt to do that.
            if (_skipNextPrompt)
            {
                _skipNextPrompt = false;
                return;
            }

            // We use the REPL as a poll to check if the debug context is active but PowerShell
            // indicates we're no longer debugging. This happens when PowerShell was used to start
            // the debugger (instead of using a Code launch configuration) via Wait-Debugger or
            // simply hitting a PSBreakpoint. We need to synchronize the state and stop the debug
            // context (and likely the debug server).
            if (!DebugContext.IsDebuggingRemoteRunspace
                && DebugContext.IsActive
                && !CurrentRunspace.Runspace.Debugger.InBreakpoint)
            {
                StopDebugContext();
            }

            try
            {
                string prompt = GetPrompt(cancellationToken);
                UI.Write(prompt);
                string userInput = InvokeReadLine(cancellationToken);

                // If the user input was empty it's because:
                //  - the user provided no input
                //  - the ReadLine task was canceled
                //  - CtrlC was sent to ReadLine (which does not propagate a cancellation)
                //
                // In any event there's nothing to run in PowerShell, so we just loop back to the
                // prompt again. However, PSReadLine will not print a newline for CtrlC, so we print
                // one, but we do not want to print one if the ReadLine task was canceled.
                if (string.IsNullOrEmpty(userInput))
                {
                    if (cancellationToken.IsCancellationRequested || LastKeyWasCtrlC())
                    {
                        UI.WriteLine();
                    }
                    // Propagate cancellation if that's what happened, since ReadLine won't.
                    // TODO: We may not need to do this at all.
                    cancellationToken.ThrowIfCancellationRequested();
                    return; // Task wasn't canceled but there was no input.
                }

                InvokeInput(userInput, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            // Propagate exceptions thrown from the debugger when quitting.
            catch (TerminateException)
            {
                throw;
            }
            // Do nothing, a break or continue statement was used outside of a loop.
            catch (FlowControlException) { }
            catch (Exception e)
            {
                UI.WriteErrorLine($"An error occurred while running the REPL loop:{Environment.NewLine}{e}");
                _logger.LogError(e, "An error occurred while running the REPL loop");
            }
            finally
            {
                // At the end of each REPL we need to complete all progress records so that the
                // progress indicator is cleared.
                if (UI is EditorServicesConsolePSHostUserInterface ui)
                {
                    ui.ResetProgress();
                }
            }
        }

        internal string GetPrompt(CancellationToken cancellationToken)
        {
            Runspace.ThrowCancelledIfUnusable();
            string prompt = DefaultPrompt;
            try
            {
                IReadOnlyList<string> results = InvokePSCommand<string>(
                    s_promptCommand,
                    executionOptions: new PowerShellExecutionOptions { ThrowOnError = false },
                    cancellationToken);

                if (results?.Count > 0)
                {
                    prompt = results[0];
                }
            }
            catch (RuntimeException) { } // Use default prompt

            if (CurrentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                // This is a PowerShell-internal method that we reuse to decorate the prompt string
                // with the remote details when remoting,
                // so the prompt changes to indicate when you're in a remote session
                prompt = Runspace.GetRemotePrompt(prompt);
            }

            return prompt;
        }

        /// <summary>
        /// This is used to write the invocation text of a command with the user's prompt so that,
        /// for example, F8 (evaluate selection) appears as if the user typed it. Used when
        /// 'WriteInputToHost' is true.
        /// </summary>
        /// <param name="command">The PSCommand we'll print after the prompt.</param>
        /// <param name="cancellationToken"></param>
        public void WriteWithPrompt(PSCommand command, CancellationToken cancellationToken)
        {
            UI.Write(GetPrompt(cancellationToken));
            UI.WriteLine(command.GetInvocationText());
        }

        private string InvokeReadLine(CancellationToken cancellationToken)
        {
            try
            {
                // TODO: If we can pass the cancellation token to ReadKey directly in PSReadLine, we
                // can remove this logic.
                _readKeyCancellationToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                return _readLineProvider.ReadLine.ReadLine(cancellationToken);
            }
            finally
            {
                _readKeyCancellationToken = CancellationToken.None;
            }
        }

        // TODO: Should we actually be directly invoking input versus queueing it as a task like everything else?
        private void InvokeInput(string input, CancellationToken cancellationToken)
        {
            SetBusy(true);

            try
            {
                // For VS Code's shell integration feature, this replaces their
                // PSConsoleHostReadLine function wrapper, as that global function is not available
                // to users of PSES, since we already wrap ReadLine ourselves.
                if (_shellIntegrationEnabled)
                {
                    System.Console.Write("\x1b]633;C\a");
                }

                InvokePSCommand(
                    new PSCommand().AddScript(input, useLocalScope: false),
                    new PowerShellExecutionOptions
                    {
                        AddToHistory = true,
                        ThrowOnError = false,
                        WriteOutputToHost = true,
                        FromRepl = true,
                    },
                    cancellationToken);
            }
            finally
            {
                SetBusy(false);
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

        private static PowerShell CreateNestedPowerShell(RunspaceInfo currentRunspace)
        {
            if (currentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                return CreatePowerShellForRunspace(currentRunspace.Runspace);
            }

            // PowerShell.CreateNestedPowerShell() sets IsNested but not IsChild
            // This means it throws due to the parent pipeline not running...
            // So we must use the RunspaceMode.CurrentRunspace option on PowerShell.Create() instead
            PowerShell pwsh = PowerShell.Create(RunspaceMode.CurrentRunspace);
            pwsh.Runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
            return pwsh;
        }

        private static PowerShell CreatePowerShellForRunspace(Runspace runspace)
        {
            PowerShell pwsh = PowerShell.Create();
            pwsh.Runspace = runspace;
            return pwsh;
        }

        private (PowerShell, EngineIntrinsics) CreateInitialPowerShell(
            HostStartupInfo hostStartupInfo,
            ReadLineProvider readLineProvider)
        {
            Runspace runspace = CreateInitialRunspace(hostStartupInfo.InitialSessionState);
            PowerShell pwsh = CreatePowerShellForRunspace(runspace);

            EngineIntrinsics engineIntrinsics = (EngineIntrinsics)runspace.SessionStateProxy.GetVariable("ExecutionContext");

            if (hostStartupInfo.ConsoleReplEnabled)
            {
                // If we've been configured to use it, or if we can't load PSReadLine, use the legacy readline
                if (hostStartupInfo.UsesLegacyReadLine || !TryLoadPSReadLine(pwsh, engineIntrinsics, out IReadLine readLine))
                {
                    readLine = new LegacyReadLine(this, ReadKey, OnPowerShellIdle);
                }

                readLineProvider.OverrideReadLine(readLine);
                System.Console.CancelKeyPress += OnCancelKeyPress;
            }

            if (VersionUtils.IsWindows)
            {
                pwsh.SetCorrectExecutionPolicy(_logger);
            }

            string commandsModulePath = Path.Combine(
                _hostInfo.BundledModulePath,
                "PowerShellEditorServices",
                "Commands",
                "PowerShellEditorServices.Commands.psd1");

            pwsh.ImportModule(commandsModulePath);

            if (hostStartupInfo.AdditionalModules?.Count > 0)
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

        /// <summary>
        /// This delegate is handed to PSReadLine and overrides similar logic within its `ReadKey`
        /// method. Essentially we're replacing PowerShell's `OnIdle` handler since the PowerShell
        /// engine isn't idle when we're sitting in PSReadLine's `ReadKey` loop. In our case we also
        /// use this idle time to process queued tasks by executing those that can run in the
        /// background, and canceling the foreground task if a queued tasks requires the foreground.
        /// Finally, if and only if we have to, we run an artificial pipeline to force PowerShell's
        /// own event processing.
        /// </summary>
        /// <param name="idleCancellationToken">
        /// This token is received from PSReadLine, and it is the ReadKey cancellation token!
        /// </param>
        internal void OnPowerShellIdle(CancellationToken idleCancellationToken)
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

            if (!runPipelineForEventProcessing && _taskQueue.IsEmpty)
            {
                return;
            }

            using (CancellationScope cancellationScope = _cancellationContext.EnterScope(isIdleScope: true, idleCancellationToken))
            {
                while (!cancellationScope.CancellationToken.IsCancellationRequested
                    && _taskQueue.TryTake(out ISynchronousTask task))
                {
                    // Tasks which require the foreground cannot run under this idle handler, so the
                    // current foreground tasks gets canceled, the new task gets prepended, and this
                    // handler returns.
                    if (CancelForegroundAndPrepend(task, isIdle: true))
                    {
                        return;
                    }

                    // If we're executing a PowerShell task, we don't need to run an extra pipeline
                    // later for events.
                    if (task is ISynchronousPowerShellTask)
                    {
                        // We don't ever want to set this to true here, just skip if it had
                        // previously been set true.
                        runPipelineForEventProcessing = false;
                    }
                    ExecuteTaskSynchronously(task, cancellationScope.CancellationToken);
                }
            }

            // We didn't end up executing anything in the background,
            // so we need to run a small artificial pipeline instead
            // to force event processing.
            if (runPipelineForEventProcessing)
            {
                InvokePSCommand(
                    new PSCommand().AddScript(
                        "[System.Diagnostics.DebuggerHidden()]param() 0",
                        useLocalScope: true),
                    executionOptions: null,
                    CancellationToken.None);
            }
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            // We need to cancel the current task.
            _cancellationContext.CancelCurrentTask();

            // If the current task was running under the debugger, we need to synchronize the
            // cancellation with our debug context (and likely the debug server). Note that if we're
            // currently stopped in a breakpoint, that means the task is _not_ under the debugger.
            if (!CurrentRunspace.Runspace.Debugger.InBreakpoint)
            {
                StopDebugContext();
            }
        }

        private ConsoleKeyInfo ReadKey(bool intercept)
        {
            // NOTE: This requests that the client (the Code extension) send a non-printing key back
            // to the terminal on stdin, emulating a user pressing a button. This allows
            // PSReadLine's thread waiting on Console.ReadKey to return. Normally we'd just cancel
            // this call, but the .NET API ReadKey is not cancellable, and is stuck until we send
            // input. This leads to a myriad of problems, but we circumvent them by pretending to
            // press a key, thus allowing ReadKey to return, and us to ignore it.
            using CancellationTokenRegistration registration = _readKeyCancellationToken.Register(
                () =>
                {
                    // For the regular Extension Terminal, we have an associated language server on
                    // which we can send a notification, and have the client subscribe an action to
                    // send a key press.
                    _languageServer?.SendNotification("powerShell/sendKeyPress");

                    // When temporary Extension Terminals are spawned, there will be no associated
                    // language server, but instead a debug adaptor server. In this case, the
                    // notification sent here will come across as a DebugSessionCustomEvent to which
                    // we can subscribe in the same way.
                    DebugServer?.SendNotification("powerShell/sendKeyPress");
                });

            // PSReadLine doesn't tell us when CtrlC was sent. So instead we keep track of the last
            // key here. This isn't functionally required, but helps us determine when the prompt
            // needs a newline added
            //
            // TODO: We may want to allow users of PSES to override this method call.
            _lastKey = System.Console.ReadKey(intercept);
            return _lastKey.Value;
        }

        internal ConsoleKeyInfo ReadKey(bool intercept, CancellationToken cancellationToken)
        {
            try
            {
                _readKeyCancellationToken = cancellationToken;
                return ReadKey(intercept);
            }
            finally
            {
                _readKeyCancellationToken = CancellationToken.None;
            }
        }

        private bool LastKeyWasCtrlC() => _lastKey.HasValue && _lastKey.Value.IsCtrlC();

        private void StopDebugContext()
        {
            // We are officially stopping the debugger.
            DebugContext.IsActive = false;

            // If the debug server is active, we need to synchronize state and stop it.
            if (DebugContext.IsDebugServerActive)
            {
                _languageServer?.SendNotification("powerShell/stopDebugger");
            }
        }

        private readonly object _replFromAnotherThread = new();

        internal void WaitForExternalDebuggerStops()
        {
            lock (_replFromAnotherThread)
            {
            }
        }

        private void OnDebuggerStopped(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
        {
            // If ErrorActionPreference is set to Break, any engine exception is going to trigger a
            // pipeline stop. Technically this is the same behavior as a standalone PowerShell
            // process, but we use pipeline stops with greater frequency due to features like run
            // selection and terminating the debugger. Without this, if the "Stop" button is pressed
            // then we hit this repeatedly.
            //
            // This info is publicly accessible via `PSDebugContext` but we'd need to access it
            // via a script. At this point in the call I'd prefer this to be as light as possible so
            // we can escape ASAP but we may want to consider switching to that at some point.
            if (!Runspace.RunspaceIsRemote && s_scriptDebuggerTriggerObjectProperty is not null)
            {
                object triggerObject = null;
                try
                {
                    triggerObject = s_scriptDebuggerTriggerObjectProperty.GetValue(Runspace.Debugger);
                }
                catch
                {
                    // Ignore all exceptions. There shouldn't be any, but as this is implementation
                    // detail that is subject to change it's best to be overly cautious.
                }

                if (triggerObject is PipelineStoppedException pse)
                {
                    throw pse;
                }
            }

            // The debugger has officially started. We use this to later check if we should stop it.
            DebugContext.IsActive = true;

            // The local debugging architecture works mostly because we control the pipeline thread,
            // but remote runspaces will trigger debugger stops on a separate thread. We lock here
            // if we're on a different thread so in then event of a transport error, we can
            // safely wind down REPL loops in a different thread.
            bool isExternal = Environment.CurrentManagedThreadId != _pipelineThread.ManagedThreadId;
            if (!isExternal)
            {
                OnDebuggerStoppedImpl(sender, debuggerStopEventArgs);
                return;
            }

            lock (_replFromAnotherThread)
            {
                OnDebuggerStoppedImpl(sender, debuggerStopEventArgs);
            }

            void OnDebuggerStoppedImpl(object sender, DebuggerStopEventArgs debuggerStopEventArgs)
            {
                // If the debug server is NOT active, we need to synchronize state and start it.
                if (!DebugContext.IsDebugServerActive)
                {
                    _languageServer?.SendNotification("powerShell/startDebugger");
                }

                DebugContext.SetDebuggerStopped(debuggerStopEventArgs);

                try
                {
                    CurrentPowerShell.WaitForRemoteOutputIfNeeded();
                    PowerShellFrameType frameBase = CurrentFrame.FrameType & PowerShellFrameType.Remote;
                    PushPowerShellAndRunLoop(
                        CreateNestedPowerShell(CurrentRunspace),
                        frameBase | PowerShellFrameType.Debug | PowerShellFrameType.Nested | PowerShellFrameType.Repl);
                    CurrentPowerShell.ResumeRemoteOutputIfNeeded();
                }
                finally
                {
                    DebugContext.SetDebuggerResumed();
                }
            }
        }

        private void OnBreakpointUpdated(object sender, BreakpointUpdatedEventArgs breakpointUpdatedEventArgs) => DebugContext.HandleBreakpointUpdated(breakpointUpdatedEventArgs);

        private void OnRunspaceStateChanged(object sender, RunspaceStateEventArgs runspaceStateEventArgs)
        {
            if (!ShouldExitExecutionLoop && !_resettingRunspace && !runspaceStateEventArgs.RunspaceStateInfo.IsUsable())
            {
                _resettingRunspace = true;
                Task _ = PopOrReinitializeRunspaceAsync().HandleErrorsAsync(_logger);
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
                new ExecutionOptions { RequiresForeground = true },
                (_) =>
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

        internal bool TryLoadPSReadLine(PowerShell pwsh, EngineIntrinsics engineIntrinsics, out IReadLine psrlReadLine)
        {
            psrlReadLine = null;
            try
            {
                PSReadLineProxy psrlProxy = PSReadLineProxy.LoadAndCreate(_loggerFactory, _hostInfo.BundledModulePath, pwsh);
                psrlReadLine = new PsrlReadLine(psrlProxy, this, engineIntrinsics, ReadKey, OnPowerShellIdle);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to load PSReadLine. Will fall back to legacy readline implementation.");
                return false;
            }
        }

        private record RunspaceFrame(
            Runspace Runspace,
            RunspaceInfo RunspaceInfo);
    }
}
