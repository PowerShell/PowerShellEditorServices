// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
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
        /// integrated console for the debug session. Default is false.
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
        /// Gets or sets optional environment variables to pass to the debuggee. The string valued
        /// properties of the 'environmentVariables' are used as key/value pairs.
        /// </summary>
        public Dictionary<string, string> Env { get; set; }
    }

    internal record PsesAttachRequestArguments : AttachRequestArguments
    {
        public string ComputerName { get; set; }

        public string ProcessId { get; set; }

        public string RunspaceId { get; set; }

        public string RunspaceName { get; set; }

        public string CustomPipeName { get; set; }
    }

    internal class LaunchAndAttachHandler : ILaunchHandler<PsesLaunchRequestArguments>, IAttachHandler<PsesAttachRequestArguments>, IOnDebugAdapterServerStarted
    {
        private static readonly Version s_minVersionForCustomPipeName = new(6, 2);
        private readonly ILogger<LaunchAndAttachHandler> _logger;
        private readonly BreakpointService _breakpointService;
        private readonly DebugService _debugService;
        private readonly IRunspaceContext _runspaceContext;
        private readonly IInternalPowerShellExecutionService _executionService;
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
            _debugStateService = debugStateService;
            _debugStateService.ServerStarted = new TaskCompletionSource<bool>();
            _remoteFileManagerService = remoteFileManagerService;
        }

        public async Task<LaunchResponse> Handle(PsesLaunchRequestArguments request, CancellationToken cancellationToken)
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
            }

            // Prepare arguments to the script - if specified
            if (request.Args?.Length > 0)
            {
                _logger.LogTrace($"Script arguments are: {string.Join(" ", request.Args)}");
            }

            // Store the launch parameters so that they can be used later
            _debugStateService.NoDebug = request.NoDebug;
            _debugStateService.ScriptToLaunch = request.Script;
            _debugStateService.Arguments = request.Args;
            _debugStateService.IsUsingTempIntegratedConsole = request.CreateTemporaryIntegratedConsole;

            if (request.CreateTemporaryIntegratedConsole
                && !string.IsNullOrEmpty(request.Script)
                && ScriptFile.IsUntitledPath(request.Script))
            {
                throw new RpcErrorException(0, "Running an Untitled file in a temporary integrated console is currently not supported.");
            }

            // If the current session is remote, map the script path to the remote
            // machine if necessary
            if (_debugStateService.ScriptToLaunch != null
                && _runspaceContext.CurrentRunspace.IsOnRemoteMachine)
            {
                _debugStateService.ScriptToLaunch =
                    _remoteFileManagerService.GetMappedPath(
                        _debugStateService.ScriptToLaunch,
                        _runspaceContext.CurrentRunspace);
            }

            // If no script is being launched, mark this as an interactive
            // debugging session
            _debugStateService.IsInteractiveDebugSession = string.IsNullOrEmpty(_debugStateService.ScriptToLaunch);

            // Sends the InitializedEvent so that the debugger will continue
            // sending configuration requests
            _debugStateService.ServerStarted.SetResult(true);

            return new LaunchResponse();
        }

        public async Task<AttachResponse> Handle(PsesAttachRequestArguments request, CancellationToken cancellationToken)
        {
            // The debugger has officially started. We use this to later check if we should stop it.
            ((PsesInternalHost)_executionService).DebugContext.IsActive = true;

            _debugStateService.IsAttachSession = true;

            _debugEventHandlerService.RegisterEventHandlers();

            bool processIdIsSet = !string.IsNullOrEmpty(request.ProcessId) && request.ProcessId != "undefined";
            bool customPipeNameIsSet = !string.IsNullOrEmpty(request.CustomPipeName) && request.CustomPipeName != "undefined";

            PowerShellVersionDetails runspaceVersion = _runspaceContext.CurrentRunspace.PowerShellVersionDetails;

            // If there are no host processes to attach to or the user cancels selection, we get a null for the process id.
            // This is not an error, just a request to stop the original "attach to" request.
            // Testing against "undefined" is a HACK because I don't know how to make "Cancel" on quick pick loading
            // to cancel on the VSCode side without sending an attachRequest with processId set to "undefined".
            if (!processIdIsSet && !customPipeNameIsSet)
            {
                _logger.LogInformation(
                    $"Attach request aborted, received {request.ProcessId} for processId.");

                throw new RpcErrorException(0, "User aborted attach to PowerShell host process.");
            }

            if (request.ComputerName != null)
            {
                if (runspaceVersion.Version.Major < 4)
                {
                    throw new RpcErrorException(0, $"Remote sessions are only available with PowerShell 4 and higher (current session is {runspaceVersion.Version}).");
                }
                else if (_runspaceContext.CurrentRunspace.RunspaceOrigin != RunspaceOrigin.Local)
                {
                    throw new RpcErrorException(0, "Cannot attach to a process in a remote session when already in a remote session.");
                }

                PSCommand enterPSSessionCommand = new PSCommand()
                    .AddCommand("Enter-PSSession")
                    .AddParameter("ComputerName", request.ComputerName);

                try
                {
                    await _executionService.ExecutePSCommandAsync(
                        enterPSSessionCommand,
                        cancellationToken,
                        PowerShellExecutionOptions.ImmediateInteractive)
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    string msg = $"Could not establish remote session to computer '{request.ComputerName}'";
                    _logger.LogError(e, msg);
                    throw new RpcErrorException(0, msg);
                }

                _debugStateService.IsRemoteAttach = true;
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

            _executionService.RunspaceChanged += RunspaceChangedHandler;

            if (processIdIsSet && int.TryParse(request.ProcessId, out int processId) && (processId > 0))
            {
                if (runspaceVersion.Version.Major < 5)
                {
                    throw new RpcErrorException(0, $"Attaching to a process is only available with PowerShell 5 and higher (current session is {runspaceVersion.Version}).");
                }

                PSCommand enterPSHostProcessCommand = new PSCommand()
                    .AddCommand("Enter-PSHostProcess")
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
                    string msg = $"Could not attach to process with Id: '{request.ProcessId}'";
                    _logger.LogError(e, msg);
                    throw new RpcErrorException(0, msg);
                }
            }
            else if (customPipeNameIsSet)
            {
                if (runspaceVersion.Version < s_minVersionForCustomPipeName)
                {
                    throw new RpcErrorException(0, $"Attaching to a process with CustomPipeName is only available with PowerShell 6.2 and higher (current session is {runspaceVersion.Version}).");
                }

                PSCommand enterPSHostProcessCommand = new PSCommand()
                    .AddCommand("Enter-PSHostProcess")
                    .AddParameter("CustomPipeName", request.CustomPipeName);

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
                    string msg = $"Could not attach to process with CustomPipeName: '{request.CustomPipeName}'";
                    _logger.LogError(e, msg);
                    throw new RpcErrorException(0, msg);
                }
            }
            else if (request.ProcessId != "current")
            {
                _logger.LogError(
                    $"Attach request failed, '{request.ProcessId}' is an invalid value for the processId.");

                throw new RpcErrorException(0, "A positive integer must be specified for the processId field.");
            }

            await runspaceChanged.Task.ConfigureAwait(false);

            // Execute the Debug-Runspace command but don't await it because it
            // will block the debug adapter initialization process.  The
            // InitializedEvent will be sent as soon as the RunspaceChanged
            // event gets fired with the attached runspace.

            PSCommand debugRunspaceCmd = new PSCommand().AddCommand("Debug-Runspace");
            if (request.RunspaceName != null)
            {
                PSCommand getRunspaceIdCommand = new PSCommand()
                    .AddCommand("Microsoft.PowerShell.Utility\\Get-Runspace")
                        .AddParameter("Name", request.RunspaceName)
                    .AddCommand("Microsoft.PowerShell.Utility\\Select-Object")
                        .AddParameter("ExpandProperty", "Id");

                try
                {
                    IEnumerable<int?> ids = await _executionService.ExecutePSCommandAsync<int?>(
                        getRunspaceIdCommand,
                        cancellationToken)
                        .ConfigureAwait(false);

                    foreach (int? id in ids)
                    {
                        _debugStateService.RunspaceId = id;
                        break;

                        // TODO: If we don't end up setting this, we should throw
                    }
                }
                catch (Exception getRunspaceException)
                {
                    _logger.LogError(
                        getRunspaceException,
                        "Unable to determine runspace to attach to. Message: {message}",
                        getRunspaceException.Message);
                }

                // TODO: We have the ID, why not just use that?
                debugRunspaceCmd.AddParameter("Name", request.RunspaceName);
            }
            else if (request.RunspaceId != null)
            {
                if (!int.TryParse(request.RunspaceId, out int runspaceId) || runspaceId <= 0)
                {
                    _logger.LogError(
                        $"Attach request failed, '{request.RunspaceId}' is an invalid value for the processId.");

                    throw new RpcErrorException(0, "A positive integer must be specified for the RunspaceId field.");
                }

                _debugStateService.RunspaceId = runspaceId;

                debugRunspaceCmd.AddParameter("Id", runspaceId);
            }
            else
            {
                _debugStateService.RunspaceId = 1;

                debugRunspaceCmd.AddParameter("Id", 1);
            }

            // Clear any existing breakpoints before proceeding
            await _breakpointService.RemoveAllBreakpointsAsync().ConfigureAwait(continueOnCapturedContext: false);

            _debugService.IsDebuggingRemoteRunspace = true;
            _debugStateService.WaitingForAttach = true;
            Task nonAwaitedTask = _executionService
                .ExecutePSCommandAsync(debugRunspaceCmd, CancellationToken.None, PowerShellExecutionOptions.ImmediateInteractive)
                .ContinueWith( OnExecutionCompletedAsync, TaskScheduler.Default);

            if (runspaceVersion.Version.Major >= 7)
            {
                _debugStateService.ServerStarted.SetResult(true);
            }
            return new AttachResponse();
        }

        // PSES follows the following flow:
        // Receive a Initialize request
        // Run Initialize handler and send response back
        // Receive a Launch/Attach request
        // Run Launch/Attach handler and send response back
        // PSES sends the initialized event at the end of the Launch/Attach handler

        // The way that the Omnisharp server works is that this OnStarted handler runs after OnInitialized
        // (after the Initialize DAP response is sent to the client) but before the _Initalized_ DAP event
        // gets sent to the client. Because of the way PSES handles breakpoints,
        // we can't send the Initialized event until _after_ we finish the Launch/Attach handler.
        // The flow above depicts this. To achieve this, we wait until _debugStateService.ServerStarted
        // is set, which will be done by the Launch/Attach handlers.
        public async Task OnStarted(IDebugAdapterServer server, CancellationToken cancellationToken) =>
            // We wait for this task to be finished before triggering the initialized message to
            // be sent to the client.
            await _debugStateService.ServerStarted.Task.ConfigureAwait(false);

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
    }
}
