//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Debugging;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class DebugAdapter
    {
        private EditorSession _editorSession;

        private bool _noDebug;
        private ILogger Logger;
        private string _arguments;
        private bool _isRemoteAttach;
        private bool _isAttachSession;
        private bool _waitingForAttach;
        private string _scriptToLaunch;
        private bool _ownsEditorSession;
        private bool _executionCompleted;
        private IMessageSender _messageSender;
        private IMessageHandlers _messageHandlers;
        private bool _isInteractiveDebugSession;
        private bool _setBreakpointInProgress;
        private RequestContext<object> _disconnectRequestContext = null;

        public DebugAdapter(
            EditorSession editorSession,
            bool ownsEditorSession,
            IMessageHandlers messageHandlers,
            IMessageSender messageSender,
            ILogger logger)
        {
            Logger = logger;
            _editorSession = editorSession;
            _messageSender = messageSender;
            _messageHandlers = messageHandlers;
            _ownsEditorSession = ownsEditorSession;
        }

        /// <summary>
        /// Gets a boolean that indicates whether the current debug adapter is
        /// using a temporary integrated console.
        /// </summary>
        public bool IsUsingTempIntegratedConsole { get; private set; }

        public void Start()
        {
            // Register all supported message types
            _messageHandlers.SetRequestHandler(InitializeRequest.Type, HandleInitializeRequestAsync);

            _messageHandlers.SetRequestHandler(LaunchRequest.Type, HandleLaunchRequestAsync);
            _messageHandlers.SetRequestHandler(AttachRequest.Type, HandleAttachRequestAsync);
            _messageHandlers.SetRequestHandler(ConfigurationDoneRequest.Type, HandleConfigurationDoneRequestAsync);
            _messageHandlers.SetRequestHandler(DisconnectRequest.Type, HandleDisconnectRequestAsync);

            _messageHandlers.SetRequestHandler(SetBreakpointsRequest.Type, HandleSetBreakpointsRequestAsync);
            _messageHandlers.SetRequestHandler(SetExceptionBreakpointsRequest.Type, HandleSetExceptionBreakpointsRequestAsync);
            _messageHandlers.SetRequestHandler(SetFunctionBreakpointsRequest.Type, HandleSetFunctionBreakpointsRequestAsync);

            _messageHandlers.SetRequestHandler(ContinueRequest.Type, HandleContinueRequestAsync);
            _messageHandlers.SetRequestHandler(NextRequest.Type, HandleNextRequestAsync);
            _messageHandlers.SetRequestHandler(StepInRequest.Type, HandleStepInRequestAsync);
            _messageHandlers.SetRequestHandler(StepOutRequest.Type, HandleStepOutRequestAsync);
            _messageHandlers.SetRequestHandler(PauseRequest.Type, HandlePauseRequestAsync);

            _messageHandlers.SetRequestHandler(ThreadsRequest.Type, HandleThreadsRequestAsync);
            _messageHandlers.SetRequestHandler(StackTraceRequest.Type, HandleStackTraceRequestAsync);
            _messageHandlers.SetRequestHandler(ScopesRequest.Type, HandleScopesRequestAsync);
            _messageHandlers.SetRequestHandler(VariablesRequest.Type, HandleVariablesRequestAsync);
            _messageHandlers.SetRequestHandler(SetVariableRequest.Type, HandleSetVariablesRequestAsync);
            _messageHandlers.SetRequestHandler(SourceRequest.Type, HandleSourceRequestAsync);
            _messageHandlers.SetRequestHandler(EvaluateRequest.Type, HandleEvaluateRequestAsync);
        }

        protected Task LaunchScriptAsync(RequestContext<object> requestContext, string scriptToLaunch)
        {
            // Is this an untitled script?
            Task launchTask = null;

            if (ScriptFile.IsUntitledPath(scriptToLaunch))
            {
                ScriptFile untitledScript = _editorSession.Workspace.GetFile(scriptToLaunch);

                launchTask = _editorSession.PowerShellContext
                    .ExecuteScriptStringAsync(untitledScript.Contents, true, true);
            }
            else
            {
                launchTask = _editorSession.PowerShellContext
                    .ExecuteScriptWithArgsAsync(scriptToLaunch, _arguments, writeInputToHost: true);
            }

            return launchTask.ContinueWith(OnExecutionCompletedAsync);
        }

        private async Task OnExecutionCompletedAsync(Task executeTask)
        {
            try
            {
                await executeTask;
            }
            catch (Exception e)
            {
                Logger.Write(
                    LogLevel.Error,
                    "Exception occurred while awaiting debug launch task.\n\n" + e.ToString());
            }

            Logger.Write(LogLevel.Verbose, "Execution completed, terminating...");

            _executionCompleted = true;

            UnregisterEventHandlers();

            if (_isAttachSession)
            {
                // Pop the sessions
                if (_editorSession.PowerShellContext.CurrentRunspace.Context == RunspaceContext.EnteredProcess)
                {
                    try
                    {
                        await _editorSession.PowerShellContext.ExecuteScriptStringAsync("Exit-PSHostProcess");

                        if (_isRemoteAttach &&
                            _editorSession.PowerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote)
                        {
                            await _editorSession.PowerShellContext.ExecuteScriptStringAsync("Exit-PSSession");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.WriteException("Caught exception while popping attached process after debugging", e);
                    }
                }
            }

            _editorSession.DebugService.IsClientAttached = false;

            if (_disconnectRequestContext != null)
            {
                // Respond to the disconnect request and stop the server
                await _disconnectRequestContext.SendResultAsync(null);
                Stop();
            }
            else
            {
                await _messageSender.SendEventAsync(
                    TerminatedEvent.Type,
                    new TerminatedEvent());
            }
        }

        protected void Stop()
        {
            Logger.Write(LogLevel.Normal, "Debug adapter is shutting down...");

            if (_editorSession != null)
            {
                _editorSession.PowerShellContext.RunspaceChanged -= powerShellContext_RunspaceChangedAsync;
                _editorSession.DebugService.DebuggerStopped -= DebugService_DebuggerStoppedAsync;
                _editorSession.PowerShellContext.DebuggerResumed -= powerShellContext_DebuggerResumedAsync;

                if (_ownsEditorSession)
                {
                    _editorSession.Dispose();
                }

                _editorSession = null;
            }

            OnSessionEnded();
        }

        #region Built-in Message Handlers

        private async Task HandleInitializeRequestAsync(
            object shutdownParams,
            RequestContext<InitializeResponseBody> requestContext)
        {
            // Clear any existing breakpoints before proceeding
            await ClearSessionBreakpointsAsync();

            // Now send the Initialize response to continue setup
            await requestContext.SendResultAsync(
                new InitializeResponseBody {
                    SupportsConfigurationDoneRequest = true,
                    SupportsFunctionBreakpoints = true,
                    SupportsConditionalBreakpoints = true,
                    SupportsHitConditionalBreakpoints = true,
                    SupportsSetVariable = true
                });
        }

        protected async Task HandleConfigurationDoneRequestAsync(
            object args,
            RequestContext<object> requestContext)
        {
            _editorSession.DebugService.IsClientAttached = true;

            if (!string.IsNullOrEmpty(_scriptToLaunch))
            {
                if (_editorSession.PowerShellContext.SessionState == PowerShellContextState.Ready)
                {
                    // Configuration is done, launch the script
                    var nonAwaitedTask = LaunchScriptAsync(requestContext, _scriptToLaunch)
                        .ConfigureAwait(continueOnCapturedContext: false);
                }
                else
                {
                    Logger.Write(
                        LogLevel.Verbose,
                        "configurationDone request called after script was already launched, skipping it.");
                }
            }

            await requestContext.SendResultAsync(null);

            if (_isInteractiveDebugSession)
            {
                if (_ownsEditorSession)
                {
                    // If this is a debug-only session, we need to start
                    // the command loop manually
                    _editorSession.HostInput.StartCommandLoop();
                }

                if (_editorSession.DebugService.IsDebuggerStopped)
                {
                    // If this is an interactive session and there's a pending breakpoint,
                    // send that information along to the debugger client
                    DebugService_DebuggerStoppedAsync(
                        this,
                        _editorSession.DebugService.CurrentDebuggerStoppedEventArgs);
                }
            }
        }

        protected async Task HandleLaunchRequestAsync(
            LaunchRequestArguments launchParams,
            RequestContext<object> requestContext)
        {
            RegisterEventHandlers();

            // Determine whether or not the working directory should be set in the PowerShellContext.
            if ((_editorSession.PowerShellContext.CurrentRunspace.Location == RunspaceLocation.Local) &&
                !_editorSession.DebugService.IsDebuggerStopped)
            {
                // Get the working directory that was passed via the debug config
                // (either via launch.json or generated via no-config debug).
                string workingDir = launchParams.Cwd;

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
                        Logger.Write(
                            LogLevel.Error,
                            $"The specified 'cwd' path is invalid: '{launchParams.Cwd}'. Error: {ex.Message}");
                    }
                }

                // If we have no working dir by this point and we are running in a temp console,
                // pick some reasonable default.
                if (string.IsNullOrEmpty(workingDir) && launchParams.CreateTemporaryIntegratedConsole)
                {
                    workingDir = Environment.CurrentDirectory;
                }

                // At this point, we will either have a working dir that should be set to cwd in
                // the PowerShellContext or the user has requested (via an empty/null cwd) that
                // the working dir should not be changed.
                if (!string.IsNullOrEmpty(workingDir))
                {
                    await _editorSession.PowerShellContext.SetWorkingDirectoryAsync(workingDir, isPathAlreadyEscaped: false);
                }

                Logger.Write(LogLevel.Verbose, $"Working dir " + (string.IsNullOrEmpty(workingDir) ? "not set." : $"set to '{workingDir}'"));
            }

            // Prepare arguments to the script - if specified
            string arguments = null;
            if ((launchParams.Args != null) && (launchParams.Args.Length > 0))
            {
                arguments = string.Join(" ", launchParams.Args);
                Logger.Write(LogLevel.Verbose, "Script arguments are: " + arguments);
            }

            // Store the launch parameters so that they can be used later
            _noDebug = launchParams.NoDebug;
            _scriptToLaunch = launchParams.Script;
            _arguments = arguments;
            IsUsingTempIntegratedConsole = launchParams.CreateTemporaryIntegratedConsole;

            // If the current session is remote, map the script path to the remote
            // machine if necessary
            if (_scriptToLaunch != null &&
                _editorSession.PowerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote)
            {
                _scriptToLaunch =
                    _editorSession.RemoteFileManager.GetMappedPath(
                        _scriptToLaunch,
                        _editorSession.PowerShellContext.CurrentRunspace);
            }

            await requestContext.SendResultAsync(null);

            // If no script is being launched, mark this as an interactive
            // debugging session
            _isInteractiveDebugSession = string.IsNullOrEmpty(_scriptToLaunch);

            // Send the InitializedEvent so that the debugger will continue
            // sending configuration requests
            await _messageSender.SendEventAsync(
                InitializedEvent.Type,
                null);
        }

        protected async Task HandleAttachRequestAsync(
            AttachRequestArguments attachParams,
            RequestContext<object> requestContext)
        {
            _isAttachSession = true;

            RegisterEventHandlers();

            bool processIdIsSet = !string.IsNullOrEmpty(attachParams.ProcessId) && attachParams.ProcessId != "undefined";
            bool customPipeNameIsSet = !string.IsNullOrEmpty(attachParams.CustomPipeName) && attachParams.CustomPipeName != "undefined";

            PowerShellVersionDetails runspaceVersion =
                _editorSession.PowerShellContext.CurrentRunspace.PowerShellVersion;

            // If there are no host processes to attach to or the user cancels selection, we get a null for the process id.
            // This is not an error, just a request to stop the original "attach to" request.
            // Testing against "undefined" is a HACK because I don't know how to make "Cancel" on quick pick loading
            // to cancel on the VSCode side without sending an attachRequest with processId set to "undefined".
            if (!processIdIsSet && !customPipeNameIsSet)
            {
                Logger.Write(
                    LogLevel.Normal,
                    $"Attach request aborted, received {attachParams.ProcessId} for processId.");

                await requestContext.SendErrorAsync(
                    "User aborted attach to PowerShell host process.");

                return;
            }

            StringBuilder errorMessages = new StringBuilder();

            if (attachParams.ComputerName != null)
            {
                if (runspaceVersion.Version.Major < 4)
                {
                    await requestContext.SendErrorAsync(
                        $"Remote sessions are only available with PowerShell 4 and higher (current session is {runspaceVersion.Version}).");

                    return;
                }
                else if (_editorSession.PowerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote)
                {
                    await requestContext.SendErrorAsync(
                        $"Cannot attach to a process in a remote session when already in a remote session.");

                    return;
                }

                await _editorSession.PowerShellContext.ExecuteScriptStringAsync(
                    $"Enter-PSSession -ComputerName \"{attachParams.ComputerName}\"",
                    errorMessages);

                if (errorMessages.Length > 0)
                {
                    await requestContext.SendErrorAsync(
                        $"Could not establish remote session to computer '{attachParams.ComputerName}'");

                    return;
                }

                _isRemoteAttach = true;
            }

            if (processIdIsSet && int.TryParse(attachParams.ProcessId, out int processId) && (processId > 0))
            {
                if (runspaceVersion.Version.Major < 5)
                {
                    await requestContext.SendErrorAsync(
                        $"Attaching to a process is only available with PowerShell 5 and higher (current session is {runspaceVersion.Version}).");
                    return;
                }

                await _editorSession.PowerShellContext.ExecuteScriptStringAsync(
                    $"Enter-PSHostProcess -Id {processId}",
                    errorMessages);

                if (errorMessages.Length > 0)
                {
                    await requestContext.SendErrorAsync(
                        $"Could not attach to process '{processId}'");

                    return;
                }
            }
            else if (customPipeNameIsSet)
            {
                if (runspaceVersion.Version.Major < 6 && runspaceVersion.Version.Minor < 2)
                {
                    await requestContext.SendErrorAsync(
                        $"Attaching to a process with CustomPipeName is only available with PowerShell 6.2 and higher (current session is {runspaceVersion.Version}).");
                    return;
                }

                await _editorSession.PowerShellContext.ExecuteScriptStringAsync(
                    $"Enter-PSHostProcess -CustomPipeName {attachParams.CustomPipeName}",
                    errorMessages);

                if (errorMessages.Length > 0)
                {
                    await requestContext.SendErrorAsync(
                        $"Could not attach to process with CustomPipeName: '{attachParams.CustomPipeName}'");

                    return;
                }
            }
            else
            {
                Logger.Write(
                    LogLevel.Error,
                    $"Attach request failed, '{attachParams.ProcessId}' is an invalid value for the processId.");

                await requestContext.SendErrorAsync(
                    "A positive integer must be specified for the processId field.");

                return;
            }

            // Clear any existing breakpoints before proceeding
            await ClearSessionBreakpointsAsync();

            // Execute the Debug-Runspace command but don't await it because it
            // will block the debug adapter initialization process.  The
            // InitializedEvent will be sent as soon as the RunspaceChanged
            // event gets fired with the attached runspace.
            int runspaceId = attachParams.RunspaceId > 0 ? attachParams.RunspaceId : 1;
            _waitingForAttach = true;
            Task nonAwaitedTask =
                _editorSession.PowerShellContext
                    .ExecuteScriptStringAsync($"\nDebug-Runspace -Id {runspaceId}")
                    .ContinueWith(OnExecutionCompletedAsync);

            await requestContext.SendResultAsync(null);
        }

        protected async Task HandleDisconnectRequestAsync(
            object disconnectParams,
            RequestContext<object> requestContext)
        {
            // In some rare cases, the EditorSession will already be disposed
            // so we shouldn't try to abort because PowerShellContext will be null
            if (_editorSession != null && _editorSession.PowerShellContext != null)
            {
                if (_executionCompleted == false)
                {
                    _disconnectRequestContext = requestContext;
                    _editorSession.PowerShellContext.AbortExecution(shouldAbortDebugSession: true);

                    if (_isInteractiveDebugSession)
                    {
                        await OnExecutionCompletedAsync(null);
                    }
                }
                else
                {
                    UnregisterEventHandlers();

                    await requestContext.SendResultAsync(null);
                    Stop();
                }
            }
        }

        protected async Task HandleSetBreakpointsRequestAsync(
            SetBreakpointsRequestArguments setBreakpointsParams,
            RequestContext<SetBreakpointsResponseBody> requestContext)
        {
            ScriptFile scriptFile = null;

            // When you set a breakpoint in the right pane of a Git diff window on a PS1 file,
            // the Source.Path comes through as Untitled-X. That's why we check for IsUntitledPath.
            if (!ScriptFile.IsUntitledPath(setBreakpointsParams.Source.Path) &&
                !_editorSession.Workspace.TryGetFile(
                    setBreakpointsParams.Source.Path,
                    out scriptFile))
            {
                string message = _noDebug ? string.Empty : "Source file could not be accessed, breakpoint not set.";
                var srcBreakpoints = setBreakpointsParams.Breakpoints
                    .Select(srcBkpt => Protocol.DebugAdapter.Breakpoint.Create(
                        srcBkpt, setBreakpointsParams.Source.Path, message, verified: _noDebug));

                // Return non-verified breakpoint message.
                await requestContext.SendResultAsync(
                    new SetBreakpointsResponseBody {
                        Breakpoints = srcBreakpoints.ToArray()
                    });

                return;
            }

            // Verify source file is a PowerShell script file.
            string fileExtension = Path.GetExtension(scriptFile?.FilePath ?? "")?.ToLower();
            if (string.IsNullOrEmpty(fileExtension) || ((fileExtension != ".ps1") && (fileExtension != ".psm1")))
            {
                Logger.Write(
                    LogLevel.Warning,
                    $"Attempted to set breakpoints on a non-PowerShell file: {setBreakpointsParams.Source.Path}");

                string message = _noDebug ? string.Empty : "Source is not a PowerShell script, breakpoint not set.";

                var srcBreakpoints = setBreakpointsParams.Breakpoints
                    .Select(srcBkpt => Protocol.DebugAdapter.Breakpoint.Create(
                        srcBkpt, setBreakpointsParams.Source.Path, message, verified: _noDebug));

                // Return non-verified breakpoint message.
                await requestContext.SendResultAsync(
                    new SetBreakpointsResponseBody
                    {
                        Breakpoints = srcBreakpoints.ToArray()
                    });

                return;
            }

            // At this point, the source file has been verified as a PowerShell script.
            var breakpointDetails = new BreakpointDetails[setBreakpointsParams.Breakpoints.Length];
            for (int i = 0; i < breakpointDetails.Length; i++)
            {
                SourceBreakpoint srcBreakpoint = setBreakpointsParams.Breakpoints[i];
                breakpointDetails[i] = BreakpointDetails.Create(
                    scriptFile.FilePath,
                    srcBreakpoint.Line,
                    srcBreakpoint.Column,
                    srcBreakpoint.Condition,
                    srcBreakpoint.HitCondition);
            }

            // If this is a "run without debugging (Ctrl+F5)" session ignore requests to set breakpoints.
            BreakpointDetails[] updatedBreakpointDetails = breakpointDetails;
            if (!_noDebug)
            {
                _setBreakpointInProgress = true;

                try
                {
                    updatedBreakpointDetails =
                        await _editorSession.DebugService.SetLineBreakpointsAsync(
                            scriptFile,
                            breakpointDetails);
                }
                catch (Exception e)
                {
                    // Log whatever the error is
                    Logger.WriteException($"Caught error while setting breakpoints in SetBreakpoints handler for file {scriptFile?.FilePath}", e);
                }
                finally
                {
                    _setBreakpointInProgress = false;
                }
            }

            await requestContext.SendResultAsync(
                new SetBreakpointsResponseBody {
                    Breakpoints =
                        updatedBreakpointDetails
                            .Select(Protocol.DebugAdapter.Breakpoint.Create)
                            .ToArray()
                });
        }

        protected async Task HandleSetFunctionBreakpointsRequestAsync(
            SetFunctionBreakpointsRequestArguments setBreakpointsParams,
            RequestContext<SetBreakpointsResponseBody> requestContext)
        {
            var breakpointDetails = new CommandBreakpointDetails[setBreakpointsParams.Breakpoints.Length];
            for (int i = 0; i < breakpointDetails.Length; i++)
            {
                FunctionBreakpoint funcBreakpoint = setBreakpointsParams.Breakpoints[i];
                breakpointDetails[i] = CommandBreakpointDetails.Create(
                    funcBreakpoint.Name,
                    funcBreakpoint.Condition,
                    funcBreakpoint.HitCondition);
            }

            // If this is a "run without debugging (Ctrl+F5)" session ignore requests to set breakpoints.
            CommandBreakpointDetails[] updatedBreakpointDetails = breakpointDetails;
            if (!_noDebug)
            {
                _setBreakpointInProgress = true;

                try
                {
                    updatedBreakpointDetails =
                        await _editorSession.DebugService.SetCommandBreakpointsAsync(
                            breakpointDetails);
                }
                catch (Exception e)
                {
                    // Log whatever the error is
                    Logger.WriteException($"Caught error while setting command breakpoints", e);
                }
                finally
                {
                    _setBreakpointInProgress = false;
                }
            }

            await requestContext.SendResultAsync(
                new SetBreakpointsResponseBody {
                    Breakpoints =
                        updatedBreakpointDetails
                            .Select(Protocol.DebugAdapter.Breakpoint.Create)
                            .ToArray()
                });
        }

        protected async Task HandleSetExceptionBreakpointsRequestAsync(
            SetExceptionBreakpointsRequestArguments setExceptionBreakpointsParams,
            RequestContext<object> requestContext)
        {
            // TODO: When support for exception breakpoints (unhandled and/or first chance)
            //       are added to the PowerShell engine, wire up the VSCode exception
            //       breakpoints here using the pattern below to prevent bug regressions.
            //if (!noDebug)
            //{
            //    setBreakpointInProgress = true;

            //    try
            //    {
            //        // Set exception breakpoints in DebugService
            //    }
            //    catch (Exception e)
            //    {
            //        // Log whatever the error is
            //        Logger.WriteException($"Caught error while setting exception breakpoints", e);
            //    }
            //    finally
            //    {
            //        setBreakpointInProgress = false;
            //    }
            //}

            await requestContext.SendResultAsync(null);
        }

        protected async Task HandleContinueRequestAsync(
            object continueParams,
            RequestContext<object> requestContext)
        {
            _editorSession.DebugService.Continue();

            await requestContext.SendResultAsync(null);
        }

        protected async Task HandleNextRequestAsync(
            object nextParams,
            RequestContext<object> requestContext)
        {
            _editorSession.DebugService.StepOver();

            await requestContext.SendResultAsync(null);
        }

        protected Task HandlePauseRequestAsync(
            object pauseParams,
            RequestContext<object> requestContext)
        {
            try
            {
                _editorSession.DebugService.Break();
            }
            catch (NotSupportedException e)
            {
                return requestContext.SendErrorAsync(e.Message);
            }

            // This request is responded to by sending the "stopped" event
            return Task.FromResult(true);
        }

        protected async Task HandleStepInRequestAsync(
            object stepInParams,
            RequestContext<object> requestContext)
        {
            _editorSession.DebugService.StepIn();

            await requestContext.SendResultAsync(null);
        }

        protected async Task HandleStepOutRequestAsync(
            object stepOutParams,
            RequestContext<object> requestContext)
        {
            _editorSession.DebugService.StepOut();

            await requestContext.SendResultAsync(null);
        }

        protected async Task HandleThreadsRequestAsync(
            object threadsParams,
            RequestContext<ThreadsResponseBody> requestContext)
        {
            await requestContext.SendResultAsync(
                new ThreadsResponseBody
                {
                    Threads = new Thread[]
                    {
                        // TODO: What do I do with these?
                        new Thread
                        {
                            Id = 1,
                            Name = "Main Thread"
                        }
                    }
                });
        }

        protected async Task HandleStackTraceRequestAsync(
            StackTraceRequestArguments stackTraceParams,
            RequestContext<StackTraceResponseBody> requestContext)
        {
            StackFrameDetails[] stackFrames =
                _editorSession.DebugService.GetStackFrames();

            // Handle a rare race condition where the adapter requests stack frames before they've
            // begun building.
            if (stackFrames == null)
            {
                await requestContext.SendResultAsync(
                    new StackTraceResponseBody
                    {
                        StackFrames = new StackFrame[0],
                        TotalFrames = 0
                    });

                return;
            }

            List<StackFrame> newStackFrames = new List<StackFrame>();

            int startFrameIndex = stackTraceParams.StartFrame ?? 0;
            int maxFrameCount = stackFrames.Length;

            // If the number of requested levels == 0 (or null), that means get all stack frames
            // after the specified startFrame index. Otherwise get all the stack frames.
            int requestedFrameCount = (stackTraceParams.Levels ?? 0);
            if (requestedFrameCount > 0)
            {
                maxFrameCount = Math.Min(maxFrameCount, startFrameIndex + requestedFrameCount);
            }

            for (int i = startFrameIndex; i < maxFrameCount; i++)
            {
                // Create the new StackFrame object with an ID that can
                // be referenced back to the current list of stack frames
                newStackFrames.Add(
                    StackFrame.Create(
                        stackFrames[i],
                        i));
            }

            await requestContext.SendResultAsync(
                new StackTraceResponseBody
                {
                    StackFrames = newStackFrames.ToArray(),
                    TotalFrames = newStackFrames.Count
                });
        }

        protected async Task HandleScopesRequestAsync(
            ScopesRequestArguments scopesParams,
            RequestContext<ScopesResponseBody> requestContext)
        {
            VariableScope[] variableScopes =
                _editorSession.DebugService.GetVariableScopes(
                    scopesParams.FrameId);

            await requestContext.SendResultAsync(
                new ScopesResponseBody
                {
                    Scopes =
                        variableScopes
                            .Select(Scope.Create)
                            .ToArray()
                });
        }

        protected async Task HandleVariablesRequestAsync(
            VariablesRequestArguments variablesParams,
            RequestContext<VariablesResponseBody> requestContext)
        {
            VariableDetailsBase[] variables =
                _editorSession.DebugService.GetVariables(
                    variablesParams.VariablesReference);

            VariablesResponseBody variablesResponse = null;

            try
            {
                variablesResponse = new VariablesResponseBody
                {
                    Variables =
                        variables
                            .Select(Variable.Create)
                            .ToArray()
                };
            }
            catch (Exception)
            {
                // TODO: This shouldn't be so broad
            }

            await requestContext.SendResultAsync(variablesResponse);
        }

        protected async Task HandleSetVariablesRequestAsync(
            SetVariableRequestArguments setVariableParams,
            RequestContext<SetVariableResponseBody> requestContext)
        {
            try
            {
                string updatedValue =
                    await _editorSession.DebugService.SetVariableAsync(
                        setVariableParams.VariablesReference,
                        setVariableParams.Name,
                        setVariableParams.Value);

                var setVariableResponse = new SetVariableResponseBody
                {
                    Value = updatedValue
                };

                await requestContext.SendResultAsync(setVariableResponse);
            }
            catch (Exception ex) when (ex is ArgumentTransformationMetadataException ||
                                       ex is InvalidPowerShellExpressionException ||
                                       ex is SessionStateUnauthorizedAccessException)
            {
                // Catch common, innocuous errors caused by the user supplying a value that can't be converted or the variable is not settable.
                Logger.Write(LogLevel.Verbose, $"Failed to set variable: {ex.Message}");
                await requestContext.SendErrorAsync(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Write(LogLevel.Error, $"Unexpected error setting variable: {ex.Message}");
                string msg =
                    $"Unexpected error: {ex.GetType().Name} - {ex.Message}  Please report this error to the PowerShellEditorServices project on GitHub.";
                await requestContext.SendErrorAsync(msg);
            }
        }

        protected Task HandleSourceRequestAsync(
            SourceRequestArguments sourceParams,
            RequestContext<SourceResponseBody> requestContext)
        {
            // TODO: Implement this message.  For now, doesn't seem to
            // be a problem that it's missing.

            return Task.FromResult(true);
        }

        protected async Task HandleEvaluateRequestAsync(
            EvaluateRequestArguments evaluateParams,
            RequestContext<EvaluateResponseBody> requestContext)
        {
            string valueString = null;
            int variableId = 0;

            bool isFromRepl =
                string.Equals(
                    evaluateParams.Context,
                    "repl",
                    StringComparison.CurrentCultureIgnoreCase);

            if (isFromRepl)
            {
                var notAwaited =
                    _editorSession
                        .PowerShellContext
                        .ExecuteScriptStringAsync(evaluateParams.Expression, false, true)
                        .ConfigureAwait(false);
            }
            else
            {
                VariableDetailsBase result = null;

                // VS Code might send this request after the debugger
                // has been resumed, return an empty result in this case.
                if (_editorSession.PowerShellContext.IsDebuggerStopped)
                {
                    // First check to see if the watch expression refers to a naked variable reference.
                    result =
                        _editorSession.DebugService.GetVariableFromExpression(evaluateParams.Expression, evaluateParams.FrameId);

                    // If the expression is not a naked variable reference, then evaluate the expression.
                    if (result == null)
                    {
                        result =
                            await _editorSession.DebugService.EvaluateExpressionAsync(
                                evaluateParams.Expression,
                                evaluateParams.FrameId,
                                isFromRepl);
                    }
                }

                if (result != null)
                {
                    valueString = result.ValueString;
                    variableId =
                        result.IsExpandable ?
                            result.Id : 0;
                }
            }

            await requestContext.SendResultAsync(
                new EvaluateResponseBody
                {
                    Result = valueString,
                    VariablesReference = variableId
                });
        }

        private async Task WriteUseIntegratedConsoleMessageAsync()
        {
            await _messageSender.SendEventAsync(
                OutputEvent.Type,
                new OutputEventBody
                {
                    Output = "\nThe Debug Console is no longer used for PowerShell debugging.  Please use the 'PowerShell Integrated Console' to execute commands in the debugger.  Run the 'PowerShell: Show Integrated Console' command to open it.",
                    Category = "stderr"
                });
        }

        private void RegisterEventHandlers()
        {
            _editorSession.PowerShellContext.RunspaceChanged += powerShellContext_RunspaceChangedAsync;
            _editorSession.DebugService.BreakpointUpdated += DebugService_BreakpointUpdatedAsync;
            _editorSession.DebugService.DebuggerStopped += DebugService_DebuggerStoppedAsync;
            _editorSession.PowerShellContext.DebuggerResumed += powerShellContext_DebuggerResumedAsync;
        }

        private void UnregisterEventHandlers()
        {
            _editorSession.PowerShellContext.RunspaceChanged -= powerShellContext_RunspaceChangedAsync;
            _editorSession.DebugService.BreakpointUpdated -= DebugService_BreakpointUpdatedAsync;
            _editorSession.DebugService.DebuggerStopped -= DebugService_DebuggerStoppedAsync;
            _editorSession.PowerShellContext.DebuggerResumed -= powerShellContext_DebuggerResumedAsync;
        }

        private async Task ClearSessionBreakpointsAsync()
        {
            try
            {
                await _editorSession.DebugService.ClearAllBreakpointsAsync();
            }
            catch (Exception e)
            {
                Logger.WriteException("Caught exception while clearing breakpoints from session", e);
            }
        }

        #endregion

        #region Event Handlers

        async void DebugService_DebuggerStoppedAsync(object sender, DebuggerStoppedEventArgs e)
        {
            // Provide the reason for why the debugger has stopped script execution.
            // See https://github.com/Microsoft/vscode/issues/3648
            // The reason is displayed in the breakpoints viewlet.  Some recommended reasons are:
            // "step", "breakpoint", "function breakpoint", "exception" and "pause".
            // We don't support exception breakpoints and for "pause", we can't distinguish
            // between stepping and the user pressing the pause/break button in the debug toolbar.
            string debuggerStoppedReason = "step";
            if (e.OriginalEvent.Breakpoints.Count > 0)
            {
                debuggerStoppedReason =
                    e.OriginalEvent.Breakpoints[0] is CommandBreakpoint
                        ? "function breakpoint"
                        : "breakpoint";
            }

            await _messageSender.SendEventAsync(
                StoppedEvent.Type,
                new StoppedEventBody
                {
                    Source = new Source
                    {
                        Path = e.ScriptPath,
                    },
                    ThreadId = 1,
                    Reason = debuggerStoppedReason
                });
        }

        async void powerShellContext_RunspaceChangedAsync(object sender, RunspaceChangedEventArgs e)
        {
            if (_waitingForAttach &&
                e.ChangeAction == RunspaceChangeAction.Enter &&
                e.NewRunspace.Context == RunspaceContext.DebuggedRunspace)
            {
                // Send the InitializedEvent so that the debugger will continue
                // sending configuration requests
                _waitingForAttach = false;
                await _messageSender.SendEventAsync(InitializedEvent.Type, null);
            }
            else if (
                e.ChangeAction == RunspaceChangeAction.Exit &&
                (_editorSession == null ||
                 _editorSession.PowerShellContext.IsDebuggerStopped))
            {
                // Exited the session while the debugger is stopped,
                // send a ContinuedEvent so that the client changes the
                // UI to appear to be running again
                await _messageSender.SendEventAsync<ContinuedEvent, Object>(
                    ContinuedEvent.Type,
                    new ContinuedEvent
                    {
                        ThreadId = 1,
                        AllThreadsContinued = true
                    });
            }
        }

        private async void powerShellContext_DebuggerResumedAsync(object sender, DebuggerResumeAction e)
        {
            await _messageSender.SendEventAsync(
                ContinuedEvent.Type,
                new ContinuedEvent
                {
                    AllThreadsContinued = true,
                    ThreadId = 1
                });
        }

        private async void DebugService_BreakpointUpdatedAsync(object sender, BreakpointUpdatedEventArgs e)
        {
            string reason = "changed";

            if (_setBreakpointInProgress)
            {
                // Don't send breakpoint update notifications when setting
                // breakpoints on behalf of the client.
                return;
            }

            switch (e.UpdateType)
            {
                case BreakpointUpdateType.Set:
                    reason = "new";
                    break;

                case BreakpointUpdateType.Removed:
                    reason = "removed";
                    break;
            }

            Protocol.DebugAdapter.Breakpoint breakpoint;
            if (e.Breakpoint is LineBreakpoint)
            {
                breakpoint = Protocol.DebugAdapter.Breakpoint.Create(BreakpointDetails.Create(e.Breakpoint));
            }
            else if (e.Breakpoint is CommandBreakpoint)
            {
                //breakpoint = Protocol.DebugAdapter.Breakpoint.Create(CommandBreakpointDetails.Create(e.Breakpoint));
                Logger.Write(LogLevel.Verbose, "Function breakpoint updated event is not supported yet");
                return;
            }
            else
            {
                Logger.Write(LogLevel.Error, $"Unrecognized breakpoint type {e.Breakpoint.GetType().FullName}");
                return;
            }

            breakpoint.Verified = e.UpdateType != BreakpointUpdateType.Disabled;

            await _messageSender.SendEventAsync(
                BreakpointEvent.Type,
                new BreakpointEvent
                {
                    Reason = reason,
                    Breakpoint = breakpoint
                });
        }

        #endregion

        #region Events

        public event EventHandler SessionEnded;

        protected virtual void OnSessionEnded()
        {
            SessionEnded?.Invoke(this, null);
        }

        #endregion
    }
}
