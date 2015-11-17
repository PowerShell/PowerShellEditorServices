//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Host
{
    internal class DebugAdapter : IMessageProcessor
    {
        private MessageDispatcher<EditorSession> messageDispatcher;

        public DebugAdapter()
        {
            this.messageDispatcher = new MessageDispatcher<EditorSession>();
        }

        public void Initialize()
        {
            // Register all supported message types

            this.AddRequestHandler(InitializeRequest.Type, this.HandleInitializeRequest);
            this.AddRequestHandler(LaunchRequest.Type, this.HandleLaunchRequest);
            this.AddRequestHandler(AttachRequest.Type, this.HandleAttachRequest);
            this.AddRequestHandler(DisconnectRequest.Type, this.HandleDisconnectRequest);

            this.AddRequestHandler(SetBreakpointsRequest.Type, this.HandleSetBreakpointsRequest);
            this.AddRequestHandler(SetExceptionBreakpointsRequest.Type, this.HandleSetExceptionBreakpointsRequest);

            this.AddRequestHandler(ContinueRequest.Type, this.HandleContinueRequest);
            this.AddRequestHandler(NextRequest.Type, this.HandleNextRequest);
            this.AddRequestHandler(StepInRequest.Type, this.HandleStepInRequest);
            this.AddRequestHandler(StepOutRequest.Type, this.HandleStepOutRequest);
            this.AddRequestHandler(PauseRequest.Type, this.HandlePauseRequest);

            this.AddRequestHandler(ThreadsRequest.Type, this.HandleThreadsRequest);
            this.AddRequestHandler(StackTraceRequest.Type, this.HandleStackTraceRequest);
            this.AddRequestHandler(ScopesRequest.Type, this.HandleScopesRequest);
            this.AddRequestHandler(VariablesRequest.Type, this.HandleVariablesRequest);
            this.AddRequestHandler(SourceRequest.Type, this.HandleSourceRequest);
            this.AddRequestHandler(EvaluateRequest.Type, this.HandleEvaluateRequest);
        }

        public void AddRequestHandler<TParams, TResult, TError>(
            RequestType<TParams, TResult, TError> requestType,
            Func<TParams, EditorSession, RequestContext<TResult, TError>, Task> requestHandler)
        {
            this.messageDispatcher.AddRequestHandler(
                requestType,
                requestHandler);
        }

        public void AddEventHandler<TParams>(
            EventType<TParams> eventType, 
            Func<TParams, EditorSession, EventContext, Task> eventHandler)
        {
            this.messageDispatcher.AddEventHandler(
                eventType,
                eventHandler);
        }
        
        public async Task ProcessMessage(
            Message messageToProcess, 
            EditorSession editorSession, 
            MessageWriter messageWriter)
        {
            await this.messageDispatcher.DispatchMessage(
                messageToProcess, 
                editorSession, 
                messageWriter);
        }

        #region Built-in Message Handlers

        protected async Task HandleInitializeRequest(
            InitializeRequestArguments initializeParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            // Send the Initialized event first so that we get breakpoints
            await requestContext.SendEvent(
                InitializedEvent.Type,
                null);

            // Now send the Initialize response to continue setup
            await requestContext.SendResult(new object());
        }

        protected async Task HandleLaunchRequest(
            LaunchRequestArguments launchParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            // Execute the given PowerShell script and send the response.
            // Note that we aren't waiting for execution to complete here
            // because the debugger could stop while the script executes.
            editorSession.powerShellContext
                .ExecuteScriptAtPath(launchParams.Program)
                .ContinueWith(
                    async (t) =>
                    {
                        Logger.Write(LogLevel.Verbose, "Execution completed, terminating...");

                        await requestContext.SendEvent(
                            TerminatedEvent.Type,
                            null);

                        // TODO: Find a way to exit more gracefully!
                        Environment.Exit(0);
                    });

            await requestContext.SendResult(null);
        }

        protected Task HandleAttachRequest(
            AttachRequestArguments attachParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            // TODO: Implement this once we support attaching to processes
            throw new NotImplementedException();
        }

        protected Task HandleDisconnectRequest(
            object disconnectParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            EventHandler<SessionStateChangedEventArgs> handler = null;

            handler =
                async (o, e) =>
                {
                    if (e.NewSessionState == PowerShellContextState.Ready)
                    {
                        await requestContext.SendResult(null);
                        editorSession.powerShellContext.SessionStateChanged -= handler;

                        // TODO: Find a way to exit more gracefully!
                        Environment.Exit(0);
                    }
                };

            editorSession.powerShellContext.SessionStateChanged += handler;
            editorSession.powerShellContext.AbortExecution();

            return Task.FromResult(true);
        }

        protected async Task HandleSetBreakpointsRequest(
            SetBreakpointsRequestArguments setBreakpointsParams,
            EditorSession editorSession,
            RequestContext<SetBreakpointsResponseBody, object> requestContext)
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
                            .Select(Breakpoint.Create)
                            .ToArray()
                });
        }

        protected async Task HandleSetExceptionBreakpointsRequest(
            SetExceptionBreakpointsRequestArguments setExceptionBreakpointsParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            // TODO: Handle this appropriately

            await requestContext.SendResult(null);
        }

        protected async Task HandleContinueRequest(
            object continueParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            editorSession.DebugService.Continue();

            await requestContext.SendResult(null);
        }

        protected async Task HandleNextRequest(
            object nextParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            editorSession.DebugService.StepOver();

            await requestContext.SendResult(null);
        }

        protected Task HandlePauseRequest(
            object pauseParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            editorSession.DebugService.Break();

            // This request is responded to by sending the "stopped" event
            return Task.FromResult(true);
        }

        protected async Task HandleStepInRequest(
            object stepInParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            editorSession.DebugService.StepIn();

            await requestContext.SendResult(null);
        }

        protected async Task HandleStepOutRequest(
            object stepOutParams,
            EditorSession editorSession,
            RequestContext<object, object> requestContext)
        {
            editorSession.DebugService.StepOut();

            await requestContext.SendResult(null);
        }

        protected async Task HandleThreadsRequest(
            object threadsParams,
            EditorSession editorSession,
            RequestContext<ThreadsResponseBody, object> requestContext)
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
            EditorSession editorSession,
            RequestContext<StackTraceResponseBody, object> requestContext)
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
                        i + 1));
            }

            await requestContext.SendResult(
                new StackTraceResponseBody
                {
                    StackFrames = newStackFrames.ToArray()
                });
        }

        protected async Task HandleScopesRequest(
            ScopesRequestArguments scopesParams,
            EditorSession editorSession,
            RequestContext<ScopesResponseBody, object> requestContext)
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
            EditorSession editorSession,
            RequestContext<VariablesResponseBody, object> requestContext)
        {
            VariableDetails[] variables =
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
            EditorSession editorSession,
            RequestContext<SourceResponseBody, object> requestContext)
        {
            // TODO: Implement this message.  For now, doesn't seem to
            // be a problem that it's missing.

            return Task.FromResult(true);
        }

        protected async Task HandleEvaluateRequest(
            EvaluateRequestArguments evaluateParams,
            EditorSession editorSession,
            RequestContext<EvaluateResponseBody, object> requestContext)
        {
            VariableDetails result =
                await editorSession.DebugService.EvaluateExpression(
                    evaluateParams.Expression,
                    evaluateParams.FrameId);

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
    }
}

