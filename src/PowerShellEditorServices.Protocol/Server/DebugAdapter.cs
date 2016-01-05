//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public class DebugAdapter : DebugAdapterBase
    {
        private EditorSession editorSession;

        public DebugAdapter() : this(new StdioServerChannel())
        {
        }

        public DebugAdapter(ChannelBase serverChannel) : base(serverChannel)
        {
            this.editorSession = new EditorSession();
            this.editorSession.StartSession();
            this.editorSession.DebugService.DebuggerStopped += this.DebugService_DebuggerStopped;
            this.editorSession.PowerShellContext.OutputWritten += this.powerShellContext_OutputWritten;
        }

        protected override void Initialize()
        {
            // Register all supported message types

            this.SetRequestHandler(LaunchRequest.Type, this.HandleLaunchRequest);
            this.SetRequestHandler(AttachRequest.Type, this.HandleAttachRequest);
            this.SetRequestHandler(DisconnectRequest.Type, this.HandleDisconnectRequest);

            this.SetRequestHandler(SetBreakpointsRequest.Type, this.HandleSetBreakpointsRequest);
            this.SetRequestHandler(SetExceptionBreakpointsRequest.Type, this.HandleSetExceptionBreakpointsRequest);

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

        protected override void Shutdown()
        {
            Logger.Write(LogLevel.Normal, "Debug adapter is shutting down...");

            if (this.editorSession != null)
            {
                this.editorSession.Dispose();
                this.editorSession = null;
            }
        }

        #region Built-in Message Handlers

        protected async Task HandleLaunchRequest(
            LaunchRequestArguments launchParams,
            RequestContext<object> requestContext)
        {
            // Execute the given PowerShell script and send the response.
            // Note that we aren't waiting for execution to complete here
            // because the debugger could stop while the script executes.
            Task executeTask =
                editorSession.PowerShellContext
                    .ExecuteScriptAtPath(launchParams.Program)
                    .ContinueWith(
                        async (t) =>
                        {
                            Logger.Write(LogLevel.Verbose, "Execution completed, terminating...");

                            await requestContext.SendEvent(
                                TerminatedEvent.Type,
                                null);

                            // Stop the server
                            this.Stop();
                        });

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
            ScriptFile scriptFile =
                editorSession.Workspace.GetFile(
                    setBreakpointsParams.Source.Path);

            BreakpointDetails[] breakpoints =
                await editorSession.DebugService.SetBreakpoints(
                    scriptFile,
                    setBreakpointsParams.Lines);

            await requestContext.SendResult(
                new SetBreakpointsResponseBody
                {
                    Breakpoints =
                        breakpoints
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
            editorSession.DebugService.Break();

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
            bool isFromRepl =
                string.Equals(
                    evaluateParams.Context,
                    "repl",
                    StringComparison.InvariantCultureIgnoreCase);

            VariableDetails result =
                await editorSession.DebugService.EvaluateExpression(
                    evaluateParams.Expression,
                    evaluateParams.FrameId,
                    isFromRepl);

            string valueString = null;
            int variableId = 0;

            if (result != null)
            {
                valueString = result.ValueString;
                variableId =
                    result.IsExpandable ?
                        result.Id : 0;
            }

            await requestContext.SendResult(
                new EvaluateResponseBody
                {
                    Result = valueString,
                    VariablesReference = variableId
                });
        }

        #endregion

        #region Event Handlers

        async void DebugService_DebuggerStopped(object sender, DebuggerStopEventArgs e)
        {
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
                    Reason = "breakpoint" // TODO: Change this based on context
                });
        }

        async void powerShellContext_OutputWritten(object sender, OutputWrittenEventArgs e)
        {
            await this.SendEvent(
                OutputEvent.Type,
                new OutputEventBody
                {
                    Output = e.OutputText + (e.IncludeNewLine ? "\r\n" : string.Empty),
                    Category = (e.OutputType == OutputType.Error) ? "stderr" : "stdout"
                });
        }

        #endregion
    }
}

