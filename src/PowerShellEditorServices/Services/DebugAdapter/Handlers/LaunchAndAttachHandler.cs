// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Remoting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;
using OmniSharp.Extensions.DebugAdapter.Protocol.Server;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal record PsesLaunchRequestArguments : LaunchRequestArguments
    {
        /// <summary>
        /// Gets or sets the absolute path to the script to debug.
        /// </summary>
        public string Script { get; set; }

        /// <summary>
        /// Gets or sets a boolean value that determines whether to automatically stop
        /// target after launch. If not specified, target does not stop.
        /// </summary>
        public bool StopOnEntry { get; set; }

        /// <summary>
        /// Gets or sets optional arguments passed to the debuggee.
        /// </summary>
        public string[] Args { get; set; }

        /// <summary>
        /// Gets or sets the working directory of the launched debuggee (specified as an absolute path).
        /// If omitted the debuggee is launched in its own directory.
        /// </summary>
        public string Cwd { get; set; }

        /// <summary>
        /// Gets or sets a boolean value that determines whether to create a temporary
        /// Extension Terminal for the debug session. Default is false.
        /// </summary>
        public bool CreateTemporaryIntegratedConsole { get; set; }

        /// <summary>
        /// Gets or sets the absolute path to the runtime executable to be used.
        /// Default is the runtime executable on the PATH.
        /// </summary>
        public string RuntimeExecutable { get; set; }

        /// <summary>
        /// Gets or sets the optional arguments passed to the runtime executable.
        /// </summary>
        public string[] RuntimeArgs { get; set; }

        /// <summary>
        /// Gets or sets the script execution mode, either "DotSource" or "Call".
        /// </summary>
        public string ExecuteMode { get; set; }

        /// <summary>
        /// Gets or sets optional environment variables to pass to the debuggee. The string valued
        /// properties of the 'environmentVariables' are used as key/value pairs.
        /// </summary>
        public Dictionary<string, string> Env { get; set; }

        /// <summary>
        /// Gets or sets the path mappings for the debugging session. This is
        /// only used when the current runspace is remote.
        /// </summary>
        public PathMapping[] PathMappings { get; set; } = [];
    }

    internal record PsesAttachRequestArguments : AttachRequestArguments
    {
        public string ComputerName { get; set; }

        public int ProcessId { get; set; }

        public int RunspaceId { get; set; }

        public string RunspaceName { get; set; }

        public string CustomPipeName { get; set; }

        /// <summary>
        /// Gets or sets the path mappings for the remote debugging session.
        /// </summary>
        public PathMapping[] PathMappings { get; set; } = [];

        /// Gets or sets a boolean value that determines whether to write the
        /// <c>PSES.Attached</c> event to the target runspace after attaching.
        /// </summary>
        public bool NotifyOnAttach { get; set; }
    }

    internal class LaunchAndAttachHandler : ILaunchHandler<PsesLaunchRequestArguments>, IAttachHandler<PsesAttachRequestArguments>, IOnDebugAdapterServerStarted
    {
        private const string _newAttachEventScript = @"
            [CmdletBinding()]
            param (
                [Parameter(Mandatory)]
                [int]
                $RunspaceId
            )

            $ErrorActionPreference = 'Stop'

            $runspace = Get-Runspace -Id $RunspaceId
            $runspace.Events.GenerateEvent(
                'PSES.Attached',
                'PSES',
                @(),
                $null)
            ";

        // TODO: We currently set `WriteInputToHost` as true, which writes our debugged commands'
        // `GetInvocationText` and that reveals some obscure implementation details we should
        // instead hide from the user with pretty strings (or perhaps not write out at all).
        //
        // This API is mostly used for F5 execution so it requires the foreground.
        private static readonly PowerShellExecutionOptions s_debuggerExecutionOptions = new()
        {
            RequiresForeground = true,
            WriteInputToHost = true,
            WriteOutputToHost = true,
            ThrowOnError = false,
            AddToHistory = true,
        };

        private static readonly int s_currentPID = System.Diagnostics.Process.GetCurrentProcess().Id;
        private static readonly Version s_minVersionForCustomPipeName = new(6, 2);
        private readonly ILogger<LaunchAndAttachHandler> _logger;
        private readonly BreakpointService _breakpointService;
        private readonly DebugService _debugService;
        private readonly IRunspaceContext _runspaceContext;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly WorkspaceService _workspaceService;
        private readonly DebugStateService _debugStateService;
        private readonly DebugEventHandlerService _debugEventHandlerService;
        private readonly IDebugAdapterServerFacade _debugAdapterServer;
        private readonly RemoteFileManagerService _remoteFileManagerService;

        public LaunchAndAttachHandler(
            ILoggerFactory factory,
            IDebugAdapterServerFacade debugAdapterServer,
            BreakpointService breakpointService,
            DebugEventHandlerService debugEventHandlerService,
            DebugService debugService,
            IRunspaceContext runspaceContext,
            IInternalPowerShellExecutionService executionService,
            WorkspaceService workspaceService,
            DebugStateService debugStateService,
            RemoteFileManagerService remoteFileManagerService)
        {
            _logger = factory.CreateLogger<LaunchAndAttachHandler>();
            _debugAdapterServer = debugAdapterServer;
            _breakpointService = breakpointService;
            _debugEventHandlerService = debugEventHandlerService;
            _debugService = debugService;
            _runspaceContext = runspaceContext;
            _executionService = executionService;
            _workspaceService = workspaceService;
            _debugStateService = debugStateService;
            // DebugServiceTests will call this with a null DebugStateService.
            if (_debugStateService is not null)
            {
                _debugStateService.ServerStarted = new TaskCompletionSource<bool>();
            }
            _remoteFileManagerService = remoteFileManagerService;
        }

        public async Task<LaunchResponse> Handle(PsesLaunchRequestArguments request, CancellationToken cancellationToken)
        {
            _debugService.PathMappings = request.PathMappings;
            try
            {
                return await HandleImpl(request, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _debugService.PathMappings = [];
                throw;
            }
        }

        public async Task<LaunchResponse> HandleImpl(PsesLaunchRequestArguments request, CancellationToken cancellationToken)
        {
            // The debugger has officially started. We use this to later check if we should stop it.
            ((PsesInternalHost)_executionService).DebugContext.IsActive = true;

            _debugEventHandlerService.RegisterEventHandlers();

            // Determine whether or not the working directory should be set in the PowerShellContext.
            if (_runspaceContext.CurrentRunspace.RunspaceOrigin == RunspaceOrigin.Local
                && !_debugService.IsDebuggerStopped)
            {
                // Get the working directory that was passed via the debug config
                // (either via launch.json or generated via no-config debug).
                string workingDir = request.Cwd;

                // Assuming we have a non-empty/null working dir, unescape the path and verify
                // the path exists and is a directory.
                if (!string.IsNullOrEmpty(workingDir))
                {
                    try
                    {
                        if ((File.GetAttributes(workingDir) & FileAttributes.Directory) != FileAttributes.Directory)
                        {
                            workingDir = Path.GetDirectoryName(workingDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        workingDir = null;
                        _logger.LogError(
                            $"The specified 'cwd' path is invalid: '{request.Cwd}'. Error: {ex.Message}");
                    }
                }

                // If we have no working dir by this point and we are running in a temp console,
                // pick some reasonable default.
                if (string.IsNullOrEmpty(workingDir) && request.CreateTemporaryIntegratedConsole)
                {
                    workingDir = Environment.CurrentDirectory;
                }

                // At this point, we will either have a working dir that should be set to cwd in
                // the PowerShellContext or the user has requested (via an empty/null cwd) that
                // the working dir should not be changed.
                if (!string.IsNullOrEmpty(workingDir))
                {
                    PSCommand setDirCommand = new PSCommand().AddCommand("Set-Location").AddParameter("LiteralPath", workingDir);
                    await _executionService.ExecutePSCommandAsync(setDirCommand, cancellationToken).ConfigureAwait(false);
                }

                _logger.LogTrace("Working dir " + (string.IsNullOrEmpty(workingDir) ? "not set." : $"set to '{workingDir}'"));

                if (!request.CreateTemporaryIntegratedConsole)
                {
                    // Start-DebugAttachSession attaches in a new temp console
                    // so we cannot set this var if already running in that
                    // console.
                    PSCommand setVariableCmd = new PSCommand().AddCommand("Set-Variable")
                        .AddParameter("Name", DebugService.PsesGlobalVariableDebugServerName)
                        .AddParameter("Value", _debugAdapterServer)
                        .AddParameter("Description", "DO NOT USE: for internal use only.")
                        .AddParameter("Scope", "Global")
                        .AddParameter("Option", "ReadOnly");

                    await _executionService.ExecutePSCommandAsync(
                        setVariableCmd,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            // Prepare arguments to the script - if specified
            if (request.Args?.Length > 0)
            {
                _logger.LogTrace($"Script arguments are: {string.Join(" ", request.Args)}");
            }

            // Store the launch parameters so that they can be used later
            _debugStateService.NoDebug = request.NoDebug;
            _debugStateService.ScriptToLaunch = GetLaunchScript(request);
            _debugStateService.Arguments = request.Args;
            _debugStateService.IsUsingTempIntegratedConsole = request.CreateTemporaryIntegratedConsole;

            // If no script is being launched, mark this as an interactive
            // debugging session
            _debugStateService.IsInteractiveDebugSession = string.IsNullOrEmpty(requestScript);

            // Sends the InitializedEvent so that the debugger will continue
            // sending configuration requests
            await _debugStateService.WaitForConfigurationDoneAsync("launch", cancellationToken).ConfigureAwait(false);

            if (!_debugStateService.IsInteractiveDebugSession)
            {
                // NOTE: This is an unawaited task because we are starting the script but not
                // waiting for it to finish.
                Task _ = LaunchScriptAsync(requestScript, request.Args, request.ExecuteMode).HandleErrorsAsync(_logger);
            }

            return new LaunchResponse();
        }

        public async Task<AttachResponse> Handle(PsesAttachRequestArguments request, CancellationToken cancellationToken)
        {
            // We want to set this as early as possible to avoid an early `StopDebugging` call in
            // DoOneRepl. There's too many places to reset this if it fails so we're wrapping the
            // entire method in a try here to reset it if failed.
            //
            // TODO: Ideally DoOneRepl would be paused until the attach is fully initialized, though
            //       the current architecture makes that challenging.
            _debugService.IsDebuggingRemoteRunspace = true;
            try
            {
                _debugService.PathMappings = request.PathMappings;
                return await HandleImpl(request, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _debugService.IsDebuggingRemoteRunspace = false;
                _debugService.PathMappings = [];
                throw;
            }
        }

        private async Task<AttachResponse> HandleImpl(PsesAttachRequestArguments request, CancellationToken cancellationToken)
        {
            // The debugger has officially started. We use this to later check if we should stop it.
            ((PsesInternalHost)_executionService).DebugContext.IsActive = true;
            _debugStateService.IsAttachSession = true;
            _debugEventHandlerService.RegisterEventHandlers();

            bool processIdIsSet = request.ProcessId != 0;
            bool customPipeNameIsSet = !string.IsNullOrEmpty(request.CustomPipeName) && request.CustomPipeName != "undefined";

            // If there are no host processes to attach to or the user cancels selection, we get a null for the process id.
            // This is not an error, just a request to stop the original "attach to" request.
            // Testing against "undefined" is a HACK because I don't know how to make "Cancel" on quick pick loading
            // to cancel on the VSCode side without sending an attachRequest with processId set to "undefined".
            if (!processIdIsSet && !customPipeNameIsSet)
            {
                string msg = $"User aborted attach to PowerShell host process: {request.ProcessId}";
                _logger.LogTrace(msg);
                throw new RpcErrorException(0, null, msg);
            }

            if (!string.IsNullOrEmpty(request.ComputerName))
            {
                await AttachToComputer(request.ComputerName, cancellationToken).ConfigureAwait(false);
            }

            // Set up a temporary runspace changed event handler so we can ensure
            // that the context switch is complete before attempting to debug
            // a runspace in the target.
            TaskCompletionSource<bool> runspaceChanged = new();

            void RunspaceChangedHandler(object s, RunspaceChangedEventArgs _)
            {
                ((IInternalPowerShellExecutionService)s).RunspaceChanged -= RunspaceChangedHandler;
                runspaceChanged.TrySetResult(true);
            }

            if (processIdIsSet)
            {
                if (request.ProcessId == s_currentPID)
                {
                    throw new RpcErrorException(0, null, $"Attaching to the Extension Terminal is not supported!");
                }

                _executionService.RunspaceChanged += RunspaceChangedHandler;
                await AttachToProcess(request.ProcessId, cancellationToken).ConfigureAwait(false);
                await runspaceChanged.Task.ConfigureAwait(false);
            }
            else if (customPipeNameIsSet)
            {
                _executionService.RunspaceChanged += RunspaceChangedHandler;
                await AttachToPipe(request.CustomPipeName, cancellationToken).ConfigureAwait(false);
                await runspaceChanged.Task.ConfigureAwait(false);
            }
            else
            {
                throw new RpcErrorException(0, null, "Invalid configuration with no process ID nor custom pipe name!");
            }

            // Execute the Debug-Runspace command but don't await it because it
            // will block the debug adapter initialization process. The
            // InitializedEvent will be sent as soon as the RunspaceChanged
            // event gets fired with the attached runspace.
            PSCommand debugRunspaceCmd = new PSCommand().AddCommand("Debug-Runspace");
            if (!string.IsNullOrEmpty(request.RunspaceName))
            {
                PSCommand psCommand = new PSCommand()
                    .AddCommand(@"Microsoft.PowerShell.Utility\Get-Runspace")
                        .AddParameter("Name", request.RunspaceName)
                    .AddCommand(@"Microsoft.PowerShell.Utility\Select-Object")
                        .AddParameter("ExpandProperty", "Id");

                IReadOnlyList<int> results = await _executionService.ExecutePSCommandAsync<int>(psCommand, cancellationToken).ConfigureAwait(false);

                if (results.Count == 0)
                {
                    throw new RpcErrorException(0, null, $"Could not find ID of runspace: {request.RunspaceName}");
                }

                // Translate the runspace name to the runspace ID.
                request.RunspaceId = results[0];
            }

            if (request.RunspaceId < 1)
            {
                throw new RpcErrorException(0, null, "A positive integer must be specified for the RunspaceId!");
            }

            _debugStateService.RunspaceId = request.RunspaceId;
            debugRunspaceCmd.AddParameter("Id", request.RunspaceId);

            // Clear any existing breakpoints before proceeding
            await _breakpointService.RemoveAllBreakpointsAsync().ConfigureAwait(continueOnCapturedContext: false);

            // The debugger is now ready to receive breakpoint requests. We do
            // this before running Debug-Runspace so the runspace is not busy
            // and can set breakpoints before the final configuration done.
            await _debugStateService.WaitForConfigurationDoneAsync("attach", cancellationToken).ConfigureAwait(false);

            if (request.NotifyOnAttach)
            {
                // This isn't perfect as there is still a race condition
                // this and Debug-Runspace setting up the debugger below but it
                // is the best we can do without changes to PowerShell.
                await _executionService.ExecutePSCommandAsync(
                    new PSCommand().AddScript(_newAttachEventScript, useLocalScope: true)
                        .AddParameter("RunspaceId", _debugStateService.RunspaceId),
                    cancellationToken).ConfigureAwait(false);
            }

            Task nonAwaitedTask = _executionService
                .ExecutePSCommandAsync(debugRunspaceCmd, CancellationToken.None, PowerShellExecutionOptions.ImmediateInteractive)
                .ContinueWith(OnExecutionCompletedAsync, TaskScheduler.Default);

            return new AttachResponse();
        }

        // NOTE: We test this function in `DebugServiceTests` so it both needs to be internal, and
        // use conditional-access on `_debugStateService` and `_debugAdapterServer` as its not set
        // by tests.
        internal async Task LaunchScriptAsync(string scriptToLaunch, string[] arguments, string requestExecuteMode)
        {
            PSCommand command;
            if (File.Exists(scriptToLaunch))
            {
                // For a saved file we just execute its path (after escaping it), with the configured operator
                // (which can't be called that because it's a reserved keyword in C#).
                string executeMode = requestExecuteMode == "Call" ? "&" : ".";
                command = PSCommandHelpers.BuildDotSourceCommandWithArguments(
                    PSCommandHelpers.EscapeScriptFilePath(scriptToLaunch), arguments, executeMode);
            }
            else // It's a URI to an untitled script, or a raw script.
            {
                bool isScriptFile = _workspaceService.TryGetFile(scriptToLaunch, out ScriptFile untitledScript);
                if (isScriptFile)
                {
                    // Parse untitled files with their `Untitled:` URI as the filename which will
                    // cache the URI and contents within the PowerShell parser. By doing this, we
                    // light up the ability to debug untitled files with line breakpoints.
                    ScriptBlockAst ast = Parser.ParseInput(
                        untitledScript.Contents,
                        untitledScript.DocumentUri.ToString(),
                        out Token[] _,
                        out ParseError[] _);

                    // In order to use utilize the parser's cache (and therefore hit line
                    // breakpoints) we need to use the AST's `ScriptBlock` object. Due to
                    // limitations in PowerShell's public API, this means we must use the
                    // `PSCommand.AddArgument(object)` method, hence this hack where we dot-source
                    // `$args[0]. Fortunately the dot-source operator maintains a stack of arguments
                    // on each invocation, so passing the user's arguments directly in the initial
                    // `AddScript` surprisingly works.
                    command = PSCommandHelpers
                        .BuildDotSourceCommandWithArguments("$args[0]", arguments)
                        .AddArgument(ast.GetScriptBlock());
                }
                else
                {
                    // Without the new APIs we can only execute the untitled script's contents.
                    // Command breakpoints and `Wait-Debugger` will work. We must wrap the script
                    // with newlines so that any included comments don't break the command.
                    command = PSCommandHelpers.BuildDotSourceCommandWithArguments(
                        string.Concat(
                            "{" + Environment.NewLine,
                            isScriptFile ? untitledScript.Contents : scriptToLaunch,
                            Environment.NewLine + "}"),
                            arguments);
                }
            }

            await _executionService.ExecutePSCommandAsync(
                command,
                CancellationToken.None,
                s_debuggerExecutionOptions).ConfigureAwait(false);

            _debugAdapterServer?.SendNotification(EventNames.Terminated);
        }

        private async Task AttachToComputer(string computerName, CancellationToken cancellationToken)
        {
            _debugStateService.IsRemoteAttach = true;

            if (_runspaceContext.CurrentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
            {
                throw new RpcErrorException(0, null, "Cannot attach to a process in a remote session when already in a remote session!");
            }

            PSCommand psCommand = new PSCommand()
                .AddCommand("Enter-PSSession")
                .AddParameter("ComputerName", computerName);

            try
            {
                await _executionService.ExecutePSCommandAsync(
                    psCommand,
                    cancellationToken,
                    PowerShellExecutionOptions.ImmediateInteractive)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                string msg = $"Could not establish remote session to computer: {computerName}";
                _logger.LogError(e, msg);
                throw new RpcErrorException(0, null, msg);
            }
        }

        private async Task AttachToProcess(int processId, CancellationToken cancellationToken)
        {
            PSCommand enterPSHostProcessCommand = new PSCommand()
                .AddCommand(@"Microsoft.PowerShell.Core\Enter-PSHostProcess")
                .AddParameter("Id", processId);

            try
            {
                await _executionService.ExecutePSCommandAsync(
                    enterPSHostProcessCommand,
                    cancellationToken,
                    PowerShellExecutionOptions.ImmediateInteractive)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                string msg = $"Could not attach to process with ID: {processId}";
                _logger.LogError(e, msg);
                throw new RpcErrorException(0, null, msg);
            }
        }

        private async Task AttachToPipe(string pipeName, CancellationToken cancellationToken)
        {
            PowerShellVersionDetails runspaceVersion = _runspaceContext.CurrentRunspace.PowerShellVersionDetails;

            if (runspaceVersion.Version < s_minVersionForCustomPipeName)
            {
                throw new RpcErrorException(0, null, $"Attaching to a process with CustomPipeName is only available with PowerShell 6.2 and higher. Current session is: {runspaceVersion.Version}");
            }

            PSCommand enterPSHostProcessCommand = new PSCommand()
                .AddCommand(@"Microsoft.PowerShell.Core\Enter-PSHostProcess")
                .AddParameter("CustomPipeName", pipeName);

            try
            {
                await _executionService.ExecutePSCommandAsync(
                    enterPSHostProcessCommand,
                    cancellationToken,
                    PowerShellExecutionOptions.ImmediateInteractive)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                string msg = $"Could not attach to process with CustomPipeName: {pipeName}";
                _logger.LogError(e, msg);
                throw new RpcErrorException(0, null, msg);
            }
        }

        // PSES follows the following flow:
        // Receive a Initialize request
        // Run Initialize handler and send response back
        // Receive a Launch/Attach request
        // Run Launch/Attach handler and send response back
        // PSES sends the initialized event at the end of the Launch/Attach handler

        // The way that the Omnisharp server works is that this OnStarted handler runs after OnInitialized
        // (after the Initialize DAP response is sent to the client) but before the _Initialized_ DAP event
        // gets sent to the client. Because of the way PSES handles breakpoints,
        // we can't send the Initialized event until _after_ we finish the Launch/Attach handler.
        // The flow above depicts this. To achieve this, we wait until _debugStateService.ServerStarted
        // is set, which will be done by the Launch/Attach handlers.
        public async Task OnStarted(IDebugAdapterServer server, CancellationToken cancellationToken) =>
            // We wait for this task to be finished before triggering the initialized message to
            // be sent to the client.
            await _debugStateService.ServerStarted.Task.ConfigureAwait(false);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "It's a wrapper.")]
        private async Task OnExecutionCompletedAsync(Task executeTask)
        {
            bool isRunspaceClosed = false;
            try
            {
                await executeTask.ConfigureAwait(false);
            }
            catch (PSRemotingTransportException)
            {
                isRunspaceClosed = true;
            }
            catch (Exception e)
            {
                _logger.LogError(
                    "Exception occurred while awaiting debug launch task.\n\n" + e.ToString());
            }

            _logger.LogTrace("Execution completed, terminating...");

            _debugStateService.ExecutionCompleted = true;

            _debugEventHandlerService.UnregisterEventHandlers();

            _debugService.IsDebuggingRemoteRunspace = false;
            _debugService.PathMappings = [];

            if (!isRunspaceClosed && _debugStateService.IsAttachSession)
            {
                // Pop the sessions
                if (_runspaceContext.CurrentRunspace.RunspaceOrigin == RunspaceOrigin.EnteredProcess)
                {
                    try
                    {
                        await _executionService.ExecutePSCommandAsync(
                            new PSCommand().AddCommand("Exit-PSHostProcess"),
                            CancellationToken.None,
                            PowerShellExecutionOptions.ImmediateInteractive)
                            .ConfigureAwait(false);

                        if (_debugStateService.IsRemoteAttach)
                        {
                            await _executionService.ExecutePSCommandAsync(new PSCommand().AddCommand("Exit-PSSession"), CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogException("Caught exception while popping attached process after debugging", e);
                    }
                }
            }

            _debugService.IsClientAttached = false;
            _debugAdapterServer.SendNotification(EventNames.Terminated);
        }

        private string GetLaunchScript(PsesLaunchRequestArguments request)
        {
            string scriptToLaunch = request.Script;
            if (request.CreateTemporaryIntegratedConsole
                && !string.IsNullOrEmpty(scriptToLaunch)
                && ScriptFile.IsUntitledPath(scriptToLaunch))
            {
                throw new RpcErrorException(0, null, "Running an Untitled file in a temporary Extension Terminal is currently not supported!");
            }

            // If the current session is remote, map the script path to the remote
            // machine if necessary
            if (scriptToLaunch is not null && _runspaceContext.CurrentRunspace.IsOnRemoteMachine)
            {
                if (_debugService.TryGetMappedRemotePath(scriptToLaunch, out string remoteMappedPath))
                {
                    scriptToLaunch = remoteMappedPath;
                }
                else
                {
                    // If the script is not mapped, we will map it to the remote path
                    // using the RemoteFileManagerService.
                    scriptToLaunch = _remoteFileManagerService.GetMappedPath(
                        scriptToLaunch,
                        _runspaceContext.CurrentRunspace);
                }
            }

            return scriptToLaunch;
        }
    }
}
