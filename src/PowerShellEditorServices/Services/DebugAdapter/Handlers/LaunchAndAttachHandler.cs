//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("launch")]
    internal interface IPsesLaunchHandler : IJsonRpcRequestHandler<PsesLaunchRequestArguments> { }

    [Serial, Method("attach")]
    internal interface IPsesAttachHandler : IJsonRpcRequestHandler<PsesAttachRequestArguments> { }

    internal class PsesLaunchRequestArguments : IRequest
    {
        /// <summary>
        /// Gets or sets the absolute path to the script to debug.
        /// </summary>
        public string Script { get; set; }

        /// <summary>
        /// Gets or sets a boolean value that indicates whether the script should be
        /// run with (false) or without (true) debugging support.
        /// </summary>
        public bool NoDebug { get; set; }

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
        /// If omitted the debuggee is lauched in its own directory.
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

    internal class PsesAttachRequestArguments : IRequest
    {
        public string ComputerName { get; set; }

        public string ProcessId { get; set; }

        public string RunspaceId { get; set; }

        public string RunspaceName { get; set; }

        public string CustomPipeName { get; set; }
    }

    internal class LaunchHandler : IPsesLaunchHandler
    {
        private readonly ILogger<LaunchHandler> _logger;
        private readonly DebugService _debugService;
        private readonly PowerShellExecutionService _executionService;
        private readonly DebugStateService _debugStateService;
        private readonly DebugEventHandlerService _debugEventHandlerService;
        private readonly IJsonRpcServer _jsonRpcServer;
        private readonly RemoteFileManagerService _remoteFileManagerService;

        public LaunchHandler(
            ILoggerFactory factory,
            IJsonRpcServer jsonRpcServer,
            DebugService debugService,
            PowerShellExecutionService executionService,
            DebugStateService debugStateService,
            DebugEventHandlerService debugEventHandlerService,
            RemoteFileManagerService remoteFileManagerService)
        {
            _logger = factory.CreateLogger<LaunchHandler>();
            _jsonRpcServer = jsonRpcServer;
            _debugService = debugService;
            _executionService = executionService;
            _debugStateService = debugStateService;
            _debugEventHandlerService = debugEventHandlerService;
            _remoteFileManagerService = remoteFileManagerService;
        }

        public async Task<Unit> Handle(PsesLaunchRequestArguments request, CancellationToken cancellationToken)
        {
            _debugEventHandlerService.RegisterEventHandlers();

            // Determine whether or not the working directory should be set in the PowerShellContext.
            if (!_executionService.CurrentRunspace.IsRemote() &&
                !_debugService.IsDebuggerStopped)
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
                    var setDirCommand = new PSCommand().AddCommand("Set-Location").AddParameter("LiteralPath", workingDir);
                    await _executionService.ExecutePSCommandAsync(setDirCommand, new PowerShellExecutionOptions(), cancellationToken);
                }

                _logger.LogTrace($"Working dir " + (string.IsNullOrEmpty(workingDir) ? "not set." : $"set to '{workingDir}'"));
            }

            // Prepare arguments to the script - if specified
            string arguments = null;
            if ((request.Args != null) && (request.Args.Length > 0))
            {
                arguments = string.Join(" ", request.Args);
                _logger.LogTrace("Script arguments are: " + arguments);
            }

            // Store the launch parameters so that they can be used later
            _debugStateService.NoDebug = request.NoDebug;
            _debugStateService.ScriptToLaunch = request.Script;
            _debugStateService.Arguments = arguments;
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
                && _executionService.CurrentRunspace.IsRemote())
            {
                _debugStateService.ScriptToLaunch =
                    _remoteFileManagerService.GetMappedPath(
                        _debugStateService.ScriptToLaunch,
                        _executionService.CurrentRunspace);
            }

            // If no script is being launched, mark this as an interactive
            // debugging session
            _debugStateService.IsInteractiveDebugSession = string.IsNullOrEmpty(_debugStateService.ScriptToLaunch);

            // Send the InitializedEvent so that the debugger will continue
            // sending configuration requests
            _jsonRpcServer.SendNotification(EventNames.Initialized);

            return Unit.Value;
        }
    }

    internal class AttachHandler : IPsesAttachHandler
    {
        private static readonly Version s_minVersionForCustomPipeName = new Version(6, 2);

        private readonly ILogger<AttachHandler> _logger;
        private readonly DebugService _debugService;
        private readonly BreakpointService _breakpointService;
        private readonly PowerShellExecutionService _executionService;
        private readonly DebugStateService _debugStateService;
        private readonly DebugEventHandlerService _debugEventHandlerService;
        private readonly IJsonRpcServer _jsonRpcServer;

        public AttachHandler(
            ILoggerFactory factory,
            IJsonRpcServer jsonRpcServer,
            DebugService debugService,
            PowerShellExecutionService executionService,
            DebugStateService debugStateService,
            BreakpointService breakpointService,
            DebugEventHandlerService debugEventHandlerService)
        {
            _logger = factory.CreateLogger<AttachHandler>();
            _jsonRpcServer = jsonRpcServer;
            _debugService = debugService;
            _breakpointService = breakpointService;
            _executionService = executionService;
            _debugStateService = debugStateService;
            _debugEventHandlerService = debugEventHandlerService;
        }

        public async Task<Unit> Handle(PsesAttachRequestArguments request, CancellationToken cancellationToken)
        {
            _debugStateService.IsAttachSession = true;

            _debugEventHandlerService.RegisterEventHandlers();

            bool processIdIsSet = !string.IsNullOrEmpty(request.ProcessId) && request.ProcessId != "undefined";
            bool customPipeNameIsSet = !string.IsNullOrEmpty(request.CustomPipeName) && request.CustomPipeName != "undefined";

            PowerShellVersionDetails runspaceVersion =
                _executionService.CurrentRunspace.PowerShellVersionDetails;

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

            StringBuilder errorMessages = new StringBuilder();

            if (request.ComputerName != null)
            {
                if (runspaceVersion.Version.Major < 4)
                {
                    throw new RpcErrorException(0, $"Remote sessions are only available with PowerShell 4 and higher (current session is {runspaceVersion.Version}).");
                }
                else if (_executionService.CurrentRunspace.IsRemote())
                {
                    throw new RpcErrorException(0, $"Cannot attach to a process in a remote session when already in a remote session.");
                }

                var enterPSSessionCommand = new PSCommand()
                    .AddCommand("Enter-PSSession")
                    .AddParameter("ComputerName", request.ComputerName);

                try
                {
                    await _executionService.ExecutePSCommandAsync(enterPSSessionCommand, new PowerShellExecutionOptions(), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    string msg = $"Could not establish remote session to computer '{request.ComputerName}'";
                    _logger.LogError(e, msg);
                    throw new RpcErrorException(0, msg);
                }

                _debugStateService.IsRemoteAttach = true;
            }

            if (processIdIsSet && int.TryParse(request.ProcessId, out int processId) && (processId > 0))
            {
                if (runspaceVersion.Version.Major < 5)
                {
                    throw new RpcErrorException(0, $"Attaching to a process is only available with PowerShell 5 and higher (current session is {runspaceVersion.Version}).");
                }

                var enterPSHostProcessCommand = new PSCommand()
                    .AddCommand("Enter-PSHostProcess")
                    .AddParameter("Id", processId);

                try
                {
                    await _executionService.ExecutePSCommandAsync(enterPSHostProcessCommand, new PowerShellExecutionOptions(), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    string msg = $"Could not attach to process '{processId}'";
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

                var enterPSHostProcessCommand = new PSCommand()
                    .AddCommand("Enter-PSHostProcess")
                    .AddParameter("CustomPipeName", request.CustomPipeName);

                try
                {
                    await _executionService.ExecutePSCommandAsync(enterPSHostProcessCommand, new PowerShellExecutionOptions(), cancellationToken);
                }
                catch (Exception e)
                {
                    string msg = $"Could not attach to process with CustomPipeName: '{request.CustomPipeName}'";
                    _logger.LogError(e, msg);
                    throw new RpcErrorException(0, msg);
                }

                if (errorMessages.Length > 0)
                {
                    throw new RpcErrorException(0, $"Could not attach to process with CustomPipeName: '{request.CustomPipeName}'");
                }
            }
            else if (request.ProcessId != "current")
            {
                _logger.LogError(
                    $"Attach request failed, '{request.ProcessId}' is an invalid value for the processId.");

                throw new RpcErrorException(0, "A positive integer must be specified for the processId field.");
            }

            // Execute the Debug-Runspace command but don't await it because it
            // will block the debug adapter initialization process.  The
            // InitializedEvent will be sent as soon as the RunspaceChanged
            // event gets fired with the attached runspace.

            var debugRunspaceCmd = new PSCommand().AddCommand("Debug-Runspace");
            if (request.RunspaceName != null)
            {
                var getRunspaceIdCommand = new PSCommand()
                    .AddCommand("Microsoft.PowerShell.Utility\\Get-Runspace")
                        .AddParameter("Name", request.RunspaceName)
                    .AddCommand("Microsoft.PowerShell.Utility\\Select-Object")
                        .AddParameter("ExpandProperty", "Id");

                IEnumerable<int?> ids = await _executionService.ExecutePSCommandAsync<int?>(getRunspaceIdCommand, new PowerShellExecutionOptions(), cancellationToken).ConfigureAwait(false);
                foreach (var id in ids)
                {
                    _debugStateService.RunspaceId = id;
                    break;

                    // TODO: If we don't end up setting this, we should throw
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

                _debugStateService.RunspaceId =  runspaceId;

                debugRunspaceCmd.AddParameter("Id", runspaceId);
            }
            else
            {
                _debugStateService.RunspaceId =  1;

                debugRunspaceCmd.AddParameter("Id", 1);
            }

            // Clear any existing breakpoints before proceeding
            await _breakpointService.RemoveAllBreakpointsAsync().ConfigureAwait(continueOnCapturedContext: false);

            _debugStateService.WaitingForAttach = true;
            Task nonAwaitedTask = _executionService
                .ExecutePSCommandAsync(debugRunspaceCmd, new PowerShellExecutionOptions(), CancellationToken.None)
                .ContinueWith(OnExecutionCompletedAsync);

            if (runspaceVersion.Version.Major >= 7)
            {
                _jsonRpcServer.SendNotification(EventNames.Initialized);
            }

            return Unit.Value;
        }

        private async Task OnExecutionCompletedAsync(Task executeTask)
        {
            try
            {
                await executeTask.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    "Exception occurred while awaiting debug launch task.\n\n" + e.ToString());
            }

            _logger.LogTrace("Execution completed, terminating...");

            _debugStateService.ExecutionCompleted = true;

            _debugEventHandlerService.UnregisterEventHandlers();

            if (_debugStateService.IsAttachSession)
            {
                // Pop the sessions
                if (_executionService.CurrentRunspace.RunspaceOrigin == RunspaceOrigin.EnteredProcess)
                {
                    try
                    {
                        await _executionService.ExecutePSCommandAsync(new PSCommand().AddCommand("Exit-PSHostProcess"), new PowerShellExecutionOptions(), CancellationToken.None);

                        if (_debugStateService.IsRemoteAttach &&
                            _executionService.CurrentRunspace.IsRemote())
                        {
                            await _executionService.ExecutePSCommandAsync(new PSCommand().AddCommand("Exit-PSSession"), new PowerShellExecutionOptions(), CancellationToken.None);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogException("Caught exception while popping attached process after debugging", e);
                    }
                }
            }

            _debugService.IsClientAttached = false;
            _jsonRpcServer.SendNotification(EventNames.Terminated);
        }
    }
}
