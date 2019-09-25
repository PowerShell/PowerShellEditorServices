﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Engine.Logging;
using Microsoft.PowerShell.EditorServices.Engine.Services;
using Microsoft.PowerShell.EditorServices.Engine.Services.PowerShellContext;
using OmniSharp.Extensions.DebugAdapter.Protocol.Events;
using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Engine.Handlers
{
    [Serial, Method("launch")]
    interface IPsesLaunchHandler : IJsonRpcRequestHandler<PsesLaunchRequestArguments> { }

    [Serial, Method("attach")]
    interface IPsesAttachHandler : IJsonRpcRequestHandler<PsesAttachRequestArguments> { }

    public class PsesLaunchRequestArguments : IRequest
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

    public class PsesAttachRequestArguments : IRequest
    {
        public string ComputerName { get; set; }

        public string ProcessId { get; set; }

        public string RunspaceId { get; set; }

        public string RunspaceName { get; set; }

        public string CustomPipeName { get; set; }
    }

    public class LaunchHandler : IPsesLaunchHandler
    {
        private readonly ILogger<LaunchHandler> _logger;
        private readonly DebugService _debugService;
        private readonly PowerShellContextService _powerShellContextService;
        private readonly DebugStateService _debugStateService;
        private readonly DebugEventHandlerService _debugEventHandlerService;
        private readonly IJsonRpcServer _jsonRpcServer;

        public LaunchHandler(
            ILoggerFactory factory,
            IJsonRpcServer jsonRpcServer,
            DebugService debugService,
            PowerShellContextService powerShellContextService,
            DebugStateService debugStateService,
            DebugEventHandlerService debugEventHandlerService)
        {
            _logger = factory.CreateLogger<LaunchHandler>();
            _jsonRpcServer = jsonRpcServer;
            _debugService = debugService;
            _powerShellContextService = powerShellContextService;
            _debugStateService = debugStateService;
            _debugEventHandlerService = debugEventHandlerService;
        }

        public async Task<Unit> Handle(PsesLaunchRequestArguments request, CancellationToken cancellationToken)
        {
            _debugEventHandlerService.RegisterEventHandlers();

            // Determine whether or not the working directory should be set in the PowerShellContext.
            if ((_powerShellContextService.CurrentRunspace.Location == RunspaceLocation.Local) &&
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
                    await _powerShellContextService.SetWorkingDirectoryAsync(workingDir, isPathAlreadyEscaped: false);
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

            // TODO: Bring this back
            // If the current session is remote, map the script path to the remote
            // machine if necessary
            //if (_scriptToLaunch != null &&
            //    _powerShellContextService.CurrentRunspace.Location == RunspaceLocation.Remote)
            //{
            //    _scriptToLaunch =
            //        _editorSession.RemoteFileManager.GetMappedPath(
            //            _scriptToLaunch,
            //            _editorSession.PowerShellContext.CurrentRunspace);
            //}

            // If no script is being launched, mark this as an interactive
            // debugging session
            _debugStateService.IsInteractiveDebugSession = string.IsNullOrEmpty(_debugStateService.ScriptToLaunch);

            // Send the InitializedEvent so that the debugger will continue
            // sending configuration requests
            _jsonRpcServer.SendNotification(EventNames.Initialized);

            return Unit.Value;
        }
    }

    public class AttachHandler : IPsesAttachHandler
    {
        private static readonly Version s_minVersionForCustomPipeName = new Version(6, 2);

        private readonly ILogger<AttachHandler> _logger;
        private readonly DebugService _debugService;
        private readonly PowerShellContextService _powerShellContextService;
        private readonly DebugStateService _debugStateService;
        private readonly DebugEventHandlerService _debugEventHandlerService;
        private readonly IJsonRpcServer _jsonRpcServer;

        public AttachHandler(
            ILoggerFactory factory,
            IJsonRpcServer jsonRpcServer,
            DebugService debugService,
            PowerShellContextService powerShellContextService,
            DebugStateService debugStateService,
            DebugEventHandlerService debugEventHandlerService)
        {
            _logger = factory.CreateLogger<AttachHandler>();
            _jsonRpcServer = jsonRpcServer;
            _debugService = debugService;
            _powerShellContextService = powerShellContextService;
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
                _powerShellContextService.CurrentRunspace.PowerShellVersion;

            // If there are no host processes to attach to or the user cancels selection, we get a null for the process id.
            // This is not an error, just a request to stop the original "attach to" request.
            // Testing against "undefined" is a HACK because I don't know how to make "Cancel" on quick pick loading
            // to cancel on the VSCode side without sending an attachRequest with processId set to "undefined".
            if (!processIdIsSet && !customPipeNameIsSet)
            {
                _logger.LogInformation(
                    $"Attach request aborted, received {request.ProcessId} for processId.");

                throw new Exception("User aborted attach to PowerShell host process.");
            }

            StringBuilder errorMessages = new StringBuilder();

            if (request.ComputerName != null)
            {
                if (runspaceVersion.Version.Major < 4)
                {
                    throw new Exception($"Remote sessions are only available with PowerShell 4 and higher (current session is {runspaceVersion.Version}).");
                }
                else if (_powerShellContextService.CurrentRunspace.Location == RunspaceLocation.Remote)
                {
                    throw new Exception($"Cannot attach to a process in a remote session when already in a remote session.");
                }

                await _powerShellContextService.ExecuteScriptStringAsync(
                    $"Enter-PSSession -ComputerName \"{request.ComputerName}\"",
                    errorMessages);

                if (errorMessages.Length > 0)
                {
                    throw new Exception($"Could not establish remote session to computer '{request.ComputerName}'");
                }

                _debugStateService.IsRemoteAttach = true;
            }

            if (processIdIsSet && int.TryParse(request.ProcessId, out int processId) && (processId > 0))
            {
                if (runspaceVersion.Version.Major < 5)
                {
                    throw new Exception($"Attaching to a process is only available with PowerShell 5 and higher (current session is {runspaceVersion.Version}).");
                }

                await _powerShellContextService.ExecuteScriptStringAsync(
                    $"Enter-PSHostProcess -Id {processId}",
                    errorMessages);

                if (errorMessages.Length > 0)
                {
                    throw new Exception($"Could not attach to process '{processId}'");
                }
            }
            else if (customPipeNameIsSet)
            {
                if (runspaceVersion.Version < s_minVersionForCustomPipeName)
                {
                    throw new Exception($"Attaching to a process with CustomPipeName is only available with PowerShell 6.2 and higher (current session is {runspaceVersion.Version}).");
                }

                await _powerShellContextService.ExecuteScriptStringAsync(
                    $"Enter-PSHostProcess -CustomPipeName {request.CustomPipeName}",
                    errorMessages);

                if (errorMessages.Length > 0)
                {
                    throw new Exception($"Could not attach to process with CustomPipeName: '{request.CustomPipeName}'");
                }
            }
            else if (request.ProcessId != "current")
            {
                _logger.LogError(
                    $"Attach request failed, '{request.ProcessId}' is an invalid value for the processId.");

                throw new Exception("A positive integer must be specified for the processId field.");
            }

            // Clear any existing breakpoints before proceeding
            await _debugService.ClearAllBreakpointsAsync().ConfigureAwait(continueOnCapturedContext: false);

            // Execute the Debug-Runspace command but don't await it because it
            // will block the debug adapter initialization process.  The
            // InitializedEvent will be sent as soon as the RunspaceChanged
            // event gets fired with the attached runspace.

            string debugRunspaceCmd;
            if (request.RunspaceName != null)
            {
                debugRunspaceCmd = $"\nDebug-Runspace -Name '{request.RunspaceName}'";
            }
            else if (request.RunspaceId != null)
            {
                if (!int.TryParse(request.RunspaceId, out int runspaceId) || runspaceId <= 0)
                {
                    _logger.LogError(
                        $"Attach request failed, '{request.RunspaceId}' is an invalid value for the processId.");

                    throw new Exception("A positive integer must be specified for the RunspaceId field.");
                }

                debugRunspaceCmd = $"\nDebug-Runspace -Id {runspaceId}";
            }
            else
            {
                debugRunspaceCmd = "\nDebug-Runspace -Id 1";
            }

            _debugStateService.WaitingForAttach = true;
            Task nonAwaitedTask = _powerShellContextService
                .ExecuteScriptStringAsync(debugRunspaceCmd)
                .ContinueWith(OnExecutionCompletedAsync);

            return Unit.Value;
        }

        private async Task OnExecutionCompletedAsync(Task executeTask)
        {
            try
            {
                await executeTask;
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
                if (_powerShellContextService.CurrentRunspace.Context == RunspaceContext.EnteredProcess)
                {
                    try
                    {
                        await _powerShellContextService.ExecuteScriptStringAsync("Exit-PSHostProcess");

                        if (_debugStateService.IsRemoteAttach &&
                            _powerShellContextService.CurrentRunspace.Location == RunspaceLocation.Remote)
                        {
                            await _powerShellContextService.ExecuteScriptStringAsync("Exit-PSSession");
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogException("Caught exception while popping attached process after debugging", e);
                    }
                }
            }

            _debugService.IsClientAttached = false;

            //if (_disconnectRequestContext != null)
            //{
            //    // Respond to the disconnect request and stop the server
            //    await _disconnectRequestContext.SendResultAsync(null);
            //    Stop();
            //    return;
            //}

            _jsonRpcServer.SendNotification(EventNames.Terminated);
        }
    }
}
