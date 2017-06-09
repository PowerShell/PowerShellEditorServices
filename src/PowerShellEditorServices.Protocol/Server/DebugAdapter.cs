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
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class DebugAdapter
    {
        private EditorSession editorSession;

        private bool noDebug;
        private ILogger Logger;
        private string arguments;
        private bool isRemoteAttach;
        private bool isAttachSession;
        private bool waitingForAttach;
        private string scriptToLaunch;
        private bool ownsEditorSession;
        private bool executionCompleted;
        private IMessageSender messageSender;
        private IMessageHandlers messageHandlers;
        private bool isInteractiveDebugSession;
        private RequestContext<object> disconnectRequestContext = null;

        public DebugAdapter(
            EditorSession editorSession,
            bool ownsEditorSession,
            IMessageHandlers messageHandlers,
            IMessageSender messageSender,
            ILogger logger)
        {
            this.Logger = logger;
            this.editorSession = editorSession;
            this.messageSender = messageSender;
            this.messageHandlers = messageHandlers;
            this.ownsEditorSession = ownsEditorSession;
        }

        public void Start()
        {
            // Register all supported message types
            this.messageHandlers.SetRequestHandler(InitializeRequest.Type, this.HandleInitializeRequest);

            this.messageHandlers.SetRequestHandler(LaunchRequest.Type, this.HandleLaunchRequest);
            this.messageHandlers.SetRequestHandler(AttachRequest.Type, this.HandleAttachRequest);
            this.messageHandlers.SetRequestHandler(ConfigurationDoneRequest.Type, this.HandleConfigurationDoneRequest);
            this.messageHandlers.SetRequestHandler(DisconnectRequest.Type, this.HandleDisconnectRequest);

            this.messageHandlers.SetRequestHandler(SetBreakpointsRequest.Type, this.HandleSetBreakpointsRequest);
            this.messageHandlers.SetRequestHandler(SetExceptionBreakpointsRequest.Type, this.HandleSetExceptionBreakpointsRequest);
            this.messageHandlers.SetRequestHandler(SetFunctionBreakpointsRequest.Type, this.HandleSetFunctionBreakpointsRequest);

            this.messageHandlers.SetRequestHandler(ContinueRequest.Type, this.HandleContinueRequest);
            this.messageHandlers.SetRequestHandler(NextRequest.Type, this.HandleNextRequest);
            this.messageHandlers.SetRequestHandler(StepInRequest.Type, this.HandleStepInRequest);
            this.messageHandlers.SetRequestHandler(StepOutRequest.Type, this.HandleStepOutRequest);
            this.messageHandlers.SetRequestHandler(PauseRequest.Type, this.HandlePauseRequest);

            this.messageHandlers.SetRequestHandler(ThreadsRequest.Type, this.HandleThreadsRequest);
            this.messageHandlers.SetRequestHandler(StackTraceRequest.Type, this.HandleStackTraceRequest);
            this.messageHandlers.SetRequestHandler(ScopesRequest.Type, this.HandleScopesRequest);
            this.messageHandlers.SetRequestHandler(VariablesRequest.Type, this.HandleVariablesRequest);
            this.messageHandlers.SetRequestHandler(SetVariableRequest.Type, this.HandleSetVariablesRequest);
            this.messageHandlers.SetRequestHandler(SourceRequest.Type, this.HandleSourceRequest);
            this.messageHandlers.SetRequestHandler(EvaluateRequest.Type, this.HandleEvaluateRequest);
        }

        protected Task LaunchScript(RequestContext<object> requestContext)
        {
            // Is this an untitled script?
            Task launchTask = null;

            if (this.scriptToLaunch.StartsWith("untitled"))
            {
                ScriptFile untitledScript =
                    this.editorSession.Workspace.GetFile(
                        this.scriptToLaunch);

                launchTask =
                    this.editorSession
                        .PowerShellContext
                        .ExecuteScriptString(untitledScript.Contents, true, true);
            }
            else
            {
                launchTask =
                    this.editorSession
                        .PowerShellContext
                        .ExecuteScriptWithArgs(this.scriptToLaunch, this.arguments, writeInputToHost: true);
            }

            return launchTask.ContinueWith(this.OnExecutionCompleted);
        }

        private async Task OnExecutionCompleted(Task executeTask)
        {
            Logger.Write(LogLevel.Verbose, "Execution completed, terminating...");

            this.executionCompleted = true;

            this.UnregisterEventHandlers();

            if (this.isAttachSession)
            {
                // Pop the sessions
                if (this.editorSession.PowerShellContext.CurrentRunspace.Context == RunspaceContext.EnteredProcess)
                {
                    try
                    {
                        await this.editorSession.PowerShellContext.ExecuteScriptString("Exit-PSHostProcess");

                        if (this.isRemoteAttach &&
                            this.editorSession.PowerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote)
                        {
                            await this.editorSession.PowerShellContext.ExecuteScriptString("Exit-PSSession");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.WriteException("Caught exception while popping attached process after debugging", e);
                    }
                }
            }

            this.editorSession.DebugService.IsClientAttached = false;

            if (this.disconnectRequestContext != null)
            {
                // Respond to the disconnect request and stop the server
                await this.disconnectRequestContext.SendResult(null);
                this.Stop();
            }
            else
            {
                await this.messageSender.SendEvent(
                    TerminatedEvent.Type,
                    new TerminatedEvent());
            }
        }

        protected void Stop()
        {
            Logger.Write(LogLevel.Normal, "Debug adapter is shutting down...");

            if (this.editorSession != null)
            {
                this.editorSession.PowerShellContext.RunspaceChanged -= this.powerShellContext_RunspaceChanged;
                this.editorSession.DebugService.DebuggerStopped -= this.DebugService_DebuggerStopped;
                this.editorSession.PowerShellContext.DebuggerResumed -= this.powerShellContext_DebuggerResumed;

                if (this.ownsEditorSession)
                {
                    this.editorSession.Dispose();
                }

                this.editorSession = null;
            }

            this.OnSessionEnded();
        }

        #region Built-in Message Handlers

        private async Task HandleInitializeRequest(
            object shutdownParams,
            RequestContext<InitializeResponseBody> requestContext)
        {
            // Now send the Initialize response to continue setup
            await requestContext.SendResult(
                new InitializeResponseBody {
                    SupportsConfigurationDoneRequest = true,
                    SupportsFunctionBreakpoints = true,
                    SupportsConditionalBreakpoints = true,
                    SupportsHitConditionalBreakpoints = true,
                    SupportsSetVariable = true
                });
        }

        protected async Task HandleConfigurationDoneRequest(
            object args,
            RequestContext<object> requestContext)
        {
            this.editorSession.DebugService.IsClientAttached = true;

            if (!string.IsNullOrEmpty(this.scriptToLaunch))
            {
                if (this.editorSession.PowerShellContext.SessionState == PowerShellContextState.Ready)
                {
                    // Configuration is done, launch the script
                    var nonAwaitedTask =
                        this.LaunchScript(requestContext)
                            .ConfigureAwait(false);
                }
                else
                {
                    Logger.Write(
                        LogLevel.Verbose,
                        "configurationDone request called after script was already launched, skipping it.");
                }
            }

            await requestContext.SendResult(null);

            if (this.isInteractiveDebugSession &&
                this.editorSession.DebugService.IsDebuggerStopped)
            {
                // If this is an interactive session and there's a pending breakpoint,
                // send that information along to the debugger client
                this.DebugService_DebuggerStopped(
                    this,
                    this.editorSession.DebugService.CurrentDebuggerStoppedEventArgs);
            }
        }

        protected async Task HandleLaunchRequest(
            LaunchRequestArguments launchParams,
            RequestContext<object> requestContext)
        {
            this.RegisterEventHandlers();

            // Set the working directory for the PowerShell runspace to the cwd passed in via launch.json.
            // In case that is null, use the the folder of the script to be executed.  If the resulting
            // working dir path is a file path then extract the directory and use that.
            string workingDir =
                launchParams.Cwd ??
                launchParams.Script ??
#pragma warning disable 618
                launchParams.Program;
#pragma warning restore 618

            if (workingDir != null)
            {
                workingDir = PowerShellContext.UnescapePath(workingDir);
                try
                {
                    if ((File.GetAttributes(workingDir) & FileAttributes.Directory) != FileAttributes.Directory)
                    {
                        workingDir = Path.GetDirectoryName(workingDir);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write(LogLevel.Error, "cwd path is invalid: " + ex.Message);

                    workingDir = null;
                }
            }

            if (workingDir == null)
            {
#if CoreCLR
                workingDir = AppContext.BaseDirectory;
#else
                workingDir = Environment.CurrentDirectory;
#endif
            }

            if (this.editorSession.PowerShellContext.CurrentRunspace.Location == RunspaceLocation.Local &&
                !this.editorSession.DebugService.IsDebuggerStopped)
            {
                await editorSession.PowerShellContext.SetWorkingDirectory(workingDir);
                Logger.Write(LogLevel.Verbose, "Working dir set to: " + workingDir);
            }

            // Prepare arguments to the script - if specified
            string arguments = null;
            if ((launchParams.Args != null) && (launchParams.Args.Length > 0))
            {
                arguments = string.Join(" ", launchParams.Args);
                Logger.Write(LogLevel.Verbose, "Script arguments are: " + arguments);
            }

            // Store the launch parameters so that they can be used later
            this.noDebug = launchParams.NoDebug;
#pragma warning disable 618
            this.scriptToLaunch = launchParams.Script ?? launchParams.Program;
#pragma warning restore 618
            this.arguments = arguments;

            // If the current session is remote, map the script path to the remote
            // machine if necessary
            if (this.scriptToLaunch != null &&
                this.editorSession.PowerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote)
            {
                this.scriptToLaunch =
                    this.editorSession.RemoteFileManager.GetMappedPath(
                        this.scriptToLaunch,
                        this.editorSession.PowerShellContext.CurrentRunspace);
            }

            await requestContext.SendResult(null);

            // If no script is being launched, mark this as an interactive
            // debugging session
            this.isInteractiveDebugSession = string.IsNullOrEmpty(this.scriptToLaunch);

            // Send the InitializedEvent so that the debugger will continue
            // sending configuration requests
            await this.messageSender.SendEvent(
                InitializedEvent.Type,
                null);
        }

        protected async Task HandleAttachRequest(
            AttachRequestArguments attachParams,
            RequestContext<object> requestContext)
        {
            this.isAttachSession = true;

            this.RegisterEventHandlers();

            // If there are no host processes to attach to or the user cancels selection, we get a null for the process id.
            // This is not an error, just a request to stop the original "attach to" request.
            // Testing against "undefined" is a HACK because I don't know how to make "Cancel" on quick pick loading
            // to cancel on the VSCode side without sending an attachRequest with processId set to "undefined".
            if (string.IsNullOrEmpty(attachParams.ProcessId) || (attachParams.ProcessId == "undefined"))
            {
                Logger.Write(
                    LogLevel.Normal,
                    $"Attach request aborted, received {attachParams.ProcessId} for processId.");

                await requestContext.SendError(
                    "User aborted attach to PowerShell host process.");

                return;
            }

            StringBuilder errorMessages = new StringBuilder();

            if (attachParams.ComputerName != null)
            {
                PowerShellVersionDetails runspaceVersion =
                    this.editorSession.PowerShellContext.CurrentRunspace.PowerShellVersion;

                if (runspaceVersion.Version.Major < 4)
                {
                    await requestContext.SendError(
                        $"Remote sessions are only available with PowerShell 4 and higher (current session is {runspaceVersion.Version}).");

                    return;
                }
                else if (this.editorSession.PowerShellContext.CurrentRunspace.Location == RunspaceLocation.Remote)
                {
                    await requestContext.SendError(
                        $"Cannot attach to a process in a remote session when already in a remote session.");

                    return;
                }

                await this.editorSession.PowerShellContext.ExecuteScriptString(
                    $"Enter-PSSession -ComputerName \"{attachParams.ComputerName}\"",
                    errorMessages);

                if (errorMessages.Length > 0)
                {
                    await requestContext.SendError(
                        $"Could not establish remote session to computer '{attachParams.ComputerName}'");

                    return;
                }

                this.isRemoteAttach = true;
            }

            if (int.TryParse(attachParams.ProcessId, out int processId) && (processId > 0))
            {
                PowerShellVersionDetails runspaceVersion =
                    this.editorSession.PowerShellContext.CurrentRunspace.PowerShellVersion;

                if (runspaceVersion.Version.Major < 5)
                {
                    await requestContext.SendError(
                        $"Attaching to a process is only available with PowerShell 5 and higher (current session is {runspaceVersion.Version}).");

                    return;
                }

                await this.editorSession.PowerShellContext.ExecuteScriptString(
                    $"Enter-PSHostProcess -Id {processId}",
                    errorMessages);

                if (errorMessages.Length > 0)
                {
                    await requestContext.SendError(
                        $"Could not attach to process '{processId}'");

                    return;
                }

                // Execute the Debug-Runspace command but don't await it because it
                // will block the debug adapter initialization process.  The
                // InitializedEvent will be sent as soon as the RunspaceChanged
                // event gets fired with the attached runspace.
                int runspaceId = attachParams.RunspaceId > 0 ? attachParams.RunspaceId : 1;
                this.waitingForAttach = true;
                Task nonAwaitedTask =
                    this.editorSession.PowerShellContext
                        .ExecuteScriptString($"\nDebug-Runspace -Id {runspaceId}")
                        .ContinueWith(this.OnExecutionCompleted);
            }
            else
            {
                Logger.Write(
                    LogLevel.Error,
                    $"Attach request failed, '{attachParams.ProcessId}' is an invalid value for the processId.");

                await requestContext.SendError(
                    "A positive integer must be specified for the processId field.");

                return;
            }

            await requestContext.SendResult(null);
        }

        protected async Task HandleDisconnectRequest(
            object disconnectParams,
            RequestContext<object> requestContext)
        {
            // In some rare cases, the EditorSession will already be disposed
            // so we shouldn't try to abort because PowerShellContext will be null
            if (this.editorSession != null && this.editorSession.PowerShellContext != null)
            {
                if (this.executionCompleted == false)
                {
                    this.disconnectRequestContext = requestContext;
                    this.editorSession.PowerShellContext.AbortExecution();

                    if (this.isInteractiveDebugSession)
                    {
                        await this.OnExecutionCompleted(null);
                    }
                }
                else
                {
                    this.UnregisterEventHandlers();

                    await requestContext.SendResult(null);
                    this.Stop();
                }
            }
        }

        protected async Task HandleSetBreakpointsRequest(
            SetBreakpointsRequestArguments setBreakpointsParams,
            RequestContext<SetBreakpointsResponseBody> requestContext)
        {
            ScriptFile scriptFile = null;

            // Fix for issue #195 - user can change name of file outside of VSCode in which case
            // VSCode sends breakpoint requests with the original filename that doesn't exist anymore.
            try
            {
                scriptFile = editorSession.Workspace.GetFile(setBreakpointsParams.Source.Path);
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                Logger.Write(
                    LogLevel.Warning,
                    $"Attempted to set breakpoints on a non-existing file: {setBreakpointsParams.Source.Path}");

                string message = this.noDebug ? string.Empty : "Source does not exist, breakpoint not set.";

                var srcBreakpoints = setBreakpointsParams.Breakpoints
                    .Select(srcBkpt => Protocol.DebugAdapter.Breakpoint.Create(
                        srcBkpt, setBreakpointsParams.Source.Path, message, verified: this.noDebug));

                // Return non-verified breakpoint message.
                await requestContext.SendResult(
                    new SetBreakpointsResponseBody {
                        Breakpoints = srcBreakpoints.ToArray()
                    });

	            return;
	        }

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
            if (!this.noDebug)
            {
                updatedBreakpointDetails =
                    await editorSession.DebugService.SetLineBreakpoints(
                        scriptFile,
                        breakpointDetails);
            }

            await requestContext.SendResult(
                new SetBreakpointsResponseBody {
                    Breakpoints =
                        updatedBreakpointDetails
                            .Select(Protocol.DebugAdapter.Breakpoint.Create)
                            .ToArray()
                });
        }

        protected async Task HandleSetFunctionBreakpointsRequest(
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
            if (!this.noDebug)
            {
                updatedBreakpointDetails =
                    await editorSession.DebugService.SetCommandBreakpoints(
                        breakpointDetails);
            }

            await requestContext.SendResult(
                new SetBreakpointsResponseBody {
                    Breakpoints =
                        updatedBreakpointDetails
                            .Select(Protocol.DebugAdapter.Breakpoint.Create)
                            .ToArray()
                });
        }

        protected async Task HandleSetExceptionBreakpointsRequest(
            SetExceptionBreakpointsRequestArguments setExceptionBreakpointsParams,
            RequestContext<object> requestContext)
        {
            // TODO: Handle this appropriately

            await requestContext.SendResult(null);
        }

        protected async Task HandleContinueRequest(
            object continueParams,
            RequestContext<object> requestContext)
        {
            editorSession.DebugService.Continue();

            await requestContext.SendResult(null);
        }

        protected async Task HandleNextRequest(
            object nextParams,
            RequestContext<object> requestContext)
        {
            editorSession.DebugService.StepOver();

            await requestContext.SendResult(null);
        }

        protected Task HandlePauseRequest(
            object pauseParams,
            RequestContext<object> requestContext)
        {
            try
            {
                editorSession.DebugService.Break();
            }
            catch (NotSupportedException e)
            {
                return requestContext.SendError(e.Message);
            }

            // This request is responded to by sending the "stopped" event
            return Task.FromResult(true);
        }

        protected async Task HandleStepInRequest(
            object stepInParams,
            RequestContext<object> requestContext)
        {
            editorSession.DebugService.StepIn();

            await requestContext.SendResult(null);
        }

        protected async Task HandleStepOutRequest(
            object stepOutParams,
            RequestContext<object> requestContext)
        {
            editorSession.DebugService.StepOut();

            await requestContext.SendResult(null);
        }

        protected async Task HandleThreadsRequest(
            object threadsParams,
            RequestContext<ThreadsResponseBody> requestContext)
        {
            await requestContext.SendResult(
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

        protected async Task HandleStackTraceRequest(
            StackTraceRequestArguments stackTraceParams,
            RequestContext<StackTraceResponseBody> requestContext)
        {
            StackFrameDetails[] stackFrames =
                editorSession.DebugService.GetStackFrames();

            List<StackFrame> newStackFrames = new List<StackFrame>();

            for (int i = 0; i < stackFrames.Length; i++)
            {
                // Create the new StackFrame object with an ID that can
                // be referenced back to the current list of stack frames
                newStackFrames.Add(
                    StackFrame.Create(
                        stackFrames[i],
                        i));
            }

            await requestContext.SendResult(
                new StackTraceResponseBody
                {
                    StackFrames = newStackFrames.ToArray()
                });
        }

        protected async Task HandleScopesRequest(
            ScopesRequestArguments scopesParams,
            RequestContext<ScopesResponseBody> requestContext)
        {
            VariableScope[] variableScopes =
                editorSession.DebugService.GetVariableScopes(
                    scopesParams.FrameId);

            await requestContext.SendResult(
                new ScopesResponseBody
                {
                    Scopes =
                        variableScopes
                            .Select(Scope.Create)
                            .ToArray()
                });
        }

        protected async Task HandleVariablesRequest(
            VariablesRequestArguments variablesParams,
            RequestContext<VariablesResponseBody> requestContext)
        {
            VariableDetailsBase[] variables =
                editorSession.DebugService.GetVariables(
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

            await requestContext.SendResult(variablesResponse);
        }

        protected async Task HandleSetVariablesRequest(
            SetVariableRequestArguments setVariableParams,
            RequestContext<SetVariableResponseBody> requestContext)
        {
            try
            {
                string updatedValue =
                    await editorSession.DebugService.SetVariable(
                        setVariableParams.VariablesReference,
                        setVariableParams.Name,
                        setVariableParams.Value);

                var setVariableResponse = new SetVariableResponseBody
                {
                    Value = updatedValue
                };

                await requestContext.SendResult(setVariableResponse);
            }
            catch (Exception ex) when (ex is ArgumentTransformationMetadataException ||
                                       ex is InvalidPowerShellExpressionException ||
                                       ex is SessionStateUnauthorizedAccessException)
            {
                // Catch common, innocuous errors caused by the user supplying a value that can't be converted or the variable is not settable.
                Logger.Write(LogLevel.Verbose, $"Failed to set variable: {ex.Message}");
                await requestContext.SendError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Write(LogLevel.Error, $"Unexpected error setting variable: {ex.Message}");
                string msg =
                    $"Unexpected error: {ex.GetType().Name} - {ex.Message}  Please report this error to the PowerShellEditorServices project on GitHub.";
                await requestContext.SendError(msg);
            }
        }

        protected Task HandleSourceRequest(
            SourceRequestArguments sourceParams,
            RequestContext<SourceResponseBody> requestContext)
        {
            // TODO: Implement this message.  For now, doesn't seem to
            // be a problem that it's missing.

            return Task.FromResult(true);
        }

        protected async Task HandleEvaluateRequest(
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
                    this.editorSession
                        .PowerShellContext
                        .ExecuteScriptString(evaluateParams.Expression, false, true)
                        .ConfigureAwait(false);
            }
            else
            {
                VariableDetails result = null;

                // VS Code might send this request after the debugger
                // has been resumed, return an empty result in this case.
                if (editorSession.PowerShellContext.IsDebuggerStopped)
                {
                    result =
                        await editorSession.DebugService.EvaluateExpression(
                            evaluateParams.Expression,
                            evaluateParams.FrameId,
                            isFromRepl);
                }

                if (result != null)
                {
                    valueString = result.ValueString;
                    variableId =
                        result.IsExpandable ?
                            result.Id : 0;
                }
            }

            await requestContext.SendResult(
                new EvaluateResponseBody
                {
                    Result = valueString,
                    VariablesReference = variableId
                });
        }

        private async Task WriteUseIntegratedConsoleMessage()
        {
            await this.messageSender.SendEvent(
                OutputEvent.Type,
                new OutputEventBody
                {
                    Output = "\nThe Debug Console is no longer used for PowerShell debugging.  Please use the 'PowerShell Integrated Console' to execute commands in the debugger.  Run the 'PowerShell: Show Integrated Console' command to open it.",
                    Category = "stderr"
                });
        }

        private void RegisterEventHandlers()
        {
            this.editorSession.PowerShellContext.RunspaceChanged += this.powerShellContext_RunspaceChanged;
            this.editorSession.DebugService.DebuggerStopped += this.DebugService_DebuggerStopped;
            this.editorSession.PowerShellContext.DebuggerResumed += this.powerShellContext_DebuggerResumed;
        }

        private void UnregisterEventHandlers()
        {
            this.editorSession.PowerShellContext.RunspaceChanged -= this.powerShellContext_RunspaceChanged;
            this.editorSession.DebugService.DebuggerStopped -= this.DebugService_DebuggerStopped;
            this.editorSession.PowerShellContext.DebuggerResumed -= this.powerShellContext_DebuggerResumed;
        }

        #endregion

        #region Event Handlers

        async void DebugService_DebuggerStopped(object sender, DebuggerStoppedEventArgs e)
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

            await this.messageSender.SendEvent(
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

        async void powerShellContext_RunspaceChanged(object sender, RunspaceChangedEventArgs e)
        {
            if (this.waitingForAttach &&
                e.ChangeAction == RunspaceChangeAction.Enter &&
                e.NewRunspace.Context == RunspaceContext.DebuggedRunspace)
            {
                // Send the InitializedEvent so that the debugger will continue
                // sending configuration requests
                this.waitingForAttach = false;
                await this.messageSender.SendEvent(InitializedEvent.Type, null);
            }
            else if (
                e.ChangeAction == RunspaceChangeAction.Exit &&
                (this.editorSession == null ||
                 this.editorSession.PowerShellContext.IsDebuggerStopped))
            {
                // Exited the session while the debugger is stopped,
                // send a ContinuedEvent so that the client changes the
                // UI to appear to be running again
                await this.messageSender.SendEvent<ContinuedEvent, Object>(
                    ContinuedEvent.Type,
                    new ContinuedEvent
                    {
                        ThreadId = 1,
                        AllThreadsContinued = true
                    });
            }
        }

        private async void powerShellContext_DebuggerResumed(object sender, DebuggerResumeAction e)
        {
            await this.messageSender.SendEvent(
                ContinuedEvent.Type,
                new ContinuedEvent
                {
                    AllThreadsContinued = true,
                    ThreadId = 1
                });
        }

        #endregion

        #region Events

        public event EventHandler SessionEnded;

        protected virtual void OnSessionEnded()
        {
            this.SessionEnded?.Invoke(this, null);
        }

        #endregion
    }
}
