//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class DebugAdapter : DebugAdapterBase
    {
        private EditorSession editorSession;
        private OutputDebouncer outputDebouncer;
        private bool isConfigurationDoneRequestComplete;
        private bool isLaunchRequestComplete;
        private bool noDebug;
        private string scriptPathToLaunch;
        private string arguments;

        public DebugAdapter(HostDetails hostDetails, ProfilePaths profilePaths)
            : this(hostDetails, profilePaths, new StdioServerChannel())
        {
        }

        public DebugAdapter(HostDetails hostDetails, ProfilePaths profilePaths, ChannelBase serverChannel)
            : base(serverChannel)
        {
            this.editorSession = new EditorSession();
            this.editorSession.StartSession(hostDetails, profilePaths);
            this.editorSession.DebugService.DebuggerStopped += this.DebugService_DebuggerStopped;
            this.editorSession.ConsoleService.OutputWritten += this.powerShellContext_OutputWritten;

            // Set up the output debouncer to throttle output event writes
            this.outputDebouncer = new OutputDebouncer(this);
        }

        protected override void Initialize()
        {
            // Register all supported message types

            this.SetRequestHandler(LaunchRequest.Type, this.HandleLaunchRequest);
            this.SetRequestHandler(AttachRequest.Type, this.HandleAttachRequest);
            this.SetRequestHandler(ConfigurationDoneRequest.Type, this.HandleConfigurationDoneRequest);
            this.SetRequestHandler(DisconnectRequest.Type, this.HandleDisconnectRequest);

            this.SetRequestHandler(SetBreakpointsRequest.Type, this.HandleSetBreakpointsRequest);
            this.SetRequestHandler(SetExceptionBreakpointsRequest.Type, this.HandleSetExceptionBreakpointsRequest);
            this.SetRequestHandler(SetFunctionBreakpointsRequest.Type, this.HandleSetFunctionBreakpointsRequest);

            this.SetRequestHandler(ContinueRequest.Type, this.HandleContinueRequest);
            this.SetRequestHandler(NextRequest.Type, this.HandleNextRequest);
            this.SetRequestHandler(StepInRequest.Type, this.HandleStepInRequest);
            this.SetRequestHandler(StepOutRequest.Type, this.HandleStepOutRequest);
            this.SetRequestHandler(PauseRequest.Type, this.HandlePauseRequest);

            this.SetRequestHandler(ThreadsRequest.Type, this.HandleThreadsRequest);
            this.SetRequestHandler(StackTraceRequest.Type, this.HandleStackTraceRequest);
            this.SetRequestHandler(ScopesRequest.Type, this.HandleScopesRequest);
            this.SetRequestHandler(VariablesRequest.Type, this.HandleVariablesRequest);
            this.SetRequestHandler(SourceRequest.Type, this.HandleSourceRequest);
            this.SetRequestHandler(EvaluateRequest.Type, this.HandleEvaluateRequest);
        }

        protected Task LaunchScript(RequestContext<object> requestContext)
        {
            return editorSession.PowerShellContext
                    .ExecuteScriptAtPath(this.scriptPathToLaunch, this.arguments)
                    .ContinueWith(
                        async (t) => {
                            Logger.Write(LogLevel.Verbose, "Execution completed, terminating...");

                            await requestContext.SendEvent(
                                TerminatedEvent.Type,
                                null);

                            // Stop the server
                            await this.Stop();

                            // Notify that the session has ended
                            this.OnSessionEnded();
                        });
        }

        protected override void Shutdown()
        {
            // Make sure remaining output is flushed before exiting
            this.outputDebouncer.Flush().Wait();

            Logger.Write(LogLevel.Normal, "Debug adapter is shutting down...");

            if (this.editorSession != null)
            {
                this.editorSession.Dispose();
                this.editorSession = null;
            }
        }

        #region Built-in Message Handlers

        protected async Task HandleConfigurationDoneRequest(
            object args,
            RequestContext<object> requestContext)
        {
            // The order of debug protocol messages apparently isn't as guaranteed as we might like.
            // Need to be able to handle the case where we get the configurationDone request after the 
            // launch request.
            if (this.isLaunchRequestComplete)
            {
                this.LaunchScript(requestContext);
            }

            this.isConfigurationDoneRequestComplete = true;

            await requestContext.SendResult(null);
        }

        protected async Task HandleLaunchRequest(
            LaunchRequestArguments launchParams,
            RequestContext<object> requestContext)
        {
            // Set the working directory for the PowerShell runspace to the cwd passed in via launch.json. 
            // In case that is null, use the the folder of the script to be executed.  If the resulting 
            // working dir path is a file path then extract the directory and use that.
            string workingDir = launchParams.Cwd ?? launchParams.Program;
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

#if NanoServer
                workingDir = AppContext.BaseDirectory;
#else
                workingDir = Environment.CurrentDirectory;
#endif
            }

            editorSession.PowerShellContext.SetWorkingDirectory(workingDir);
            Logger.Write(LogLevel.Verbose, "Working dir set to: " + workingDir);

            // Prepare arguments to the script - if specified
            string arguments = null;
            if ((launchParams.Args != null) && (launchParams.Args.Length > 0))
            {
                arguments = string.Join(" ", launchParams.Args);
                Logger.Write(LogLevel.Verbose, "Script arguments are: " + arguments);
            }

            // We may not actually launch the script in response to this
            // request unless it comes after the configurationDone request. 
            // If the launch request comes first, then stash the launch
            // params so that the subsequent configurationDone request handler 
            // can launch the script. 
            this.noDebug = launchParams.NoDebug;
            this.scriptPathToLaunch = launchParams.Program;
            this.arguments = arguments;

            // The order of debug protocol messages apparently isn't as guaranteed as we might like.
            // Need to be able to handle the case where we get the launch request after the 
            // configurationDone request.
            if (this.isConfigurationDoneRequestComplete)
            {
                this.LaunchScript(requestContext);
            }

            this.isLaunchRequestComplete = true;

            await requestContext.SendResult(null);
        }

        protected Task HandleAttachRequest(
            AttachRequestArguments attachParams,
            RequestContext<object> requestContext)
        {
            // TODO: Implement this once we support attaching to processes
            throw new NotImplementedException();
        }

        protected Task HandleDisconnectRequest(
            object disconnectParams,
            RequestContext<object> requestContext)
        {
            EventHandler<SessionStateChangedEventArgs> handler = null;

            handler =
                async (o, e) =>
                {
                    if (e.NewSessionState == PowerShellContextState.Ready)
                    {
                        await requestContext.SendResult(null);
                        editorSession.PowerShellContext.SessionStateChanged -= handler;

                        // Stop the server
                        this.Stop();
                    }
                };

            editorSession.PowerShellContext.SessionStateChanged += handler;
            editorSession.PowerShellContext.AbortExecution();

            return Task.FromResult(true);
        }

        protected async Task HandleSetBreakpointsRequest(
            SetBreakpointsRequestArguments setBreakpointsParams,
            RequestContext<SetBreakpointsResponseBody> requestContext)
        {
            ScriptFile scriptFile;

            // Fix for issue #195 - user can change name of file outside of VSCode in which case
            // VSCode sends breakpoint requests with the original filename that doesn't exist anymore.
            try
            {
                scriptFile = editorSession.Workspace.GetFile(setBreakpointsParams.Source.Path);
            }
            catch (FileNotFoundException)
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
                    srcBreakpoint.Condition);
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
                    funcBreakpoint.Condition);
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
                // Send the input through the console service
                editorSession.ConsoleService.ExecuteCommand(
                    evaluateParams.Expression,
                    false);
            }
            else
            {
                VariableDetails result =
                    await editorSession.DebugService.EvaluateExpression(
                        evaluateParams.Expression,
                        evaluateParams.FrameId,
                        isFromRepl);

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

        #endregion

        #region Events

        public event EventHandler SessionEnded;

        protected virtual void OnSessionEnded()
        {
            this.SessionEnded?.Invoke(this, null);
        }

        #endregion

        #region Event Handlers

        async void DebugService_DebuggerStopped(object sender, DebuggerStopEventArgs e)
        {
            // Flush pending output before sending the event
            await this.outputDebouncer.Flush();

            // Provide the reason for why the debugger has stopped script execution.
            // See https://github.com/Microsoft/vscode/issues/3648
            // The reason is displayed in the breakpoints viewlet.  Some recommended reasons are: 
            // "step", "breakpoint", "function breakpoint", "exception" and "pause".
            // We don't support exception breakpoints and for "pause", we can't distinguish 
            // between stepping and the user pressing the pause/break button in the debug toolbar.
            string debuggerStoppedReason = "step";
            if (e.Breakpoints.Count > 0)
            {
                debuggerStoppedReason =
                    e.Breakpoints[0] is CommandBreakpoint
                        ? "function breakpoint"
                        : "breakpoint";
            }

            await this.SendEvent(
                StoppedEvent.Type,
                new StoppedEventBody
                {
                    Source = new Source
                    {
                        Path = e.InvocationInfo.ScriptName,
                    },
                    Line = e.InvocationInfo.ScriptLineNumber,
                    Column = e.InvocationInfo.OffsetInLine,
                    ThreadId = 1, // TODO: Change this based on context
                    Reason = debuggerStoppedReason
                });
        }

        async void powerShellContext_OutputWritten(object sender, OutputWrittenEventArgs e)
        {
            // Queue the output for writing
            await this.outputDebouncer.Invoke(e);
        }

        #endregion
    }
}
