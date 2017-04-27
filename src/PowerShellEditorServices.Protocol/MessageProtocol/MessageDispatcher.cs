//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class MessageDispatcher
    {
        #region Fields

        private ChannelBase protocolChannel;
        private AsyncContextThread messageLoopThread;

        private Dictionary<string, Func<Message, MessageWriter, Task>> requestHandlers =
            new Dictionary<string, Func<Message, MessageWriter, Task>>();

        private Dictionary<string, Func<Message, MessageWriter, Task>> eventHandlers =
            new Dictionary<string, Func<Message, MessageWriter, Task>>();

        private Action<Message> responseHandler;

        private CancellationTokenSource messageLoopCancellationToken =
            new CancellationTokenSource();

        #endregion

        #region Properties

        public SynchronizationContext SynchronizationContext { get; private set; }

        public bool InMessageLoopThread
        {
            get
            {
                // We're in the same thread as the message loop if the
                // current synchronization context equals the one we
                // know.
                return SynchronizationContext.Current == this.SynchronizationContext;
            }
        }

        protected MessageReader MessageReader { get; private set; }

        protected MessageWriter MessageWriter { get; private set; }


        #endregion

        #region Constructors

        public MessageDispatcher(ChannelBase protocolChannel)
        {
            this.protocolChannel = protocolChannel;
        }

        #endregion

        #region Public Methods

        public void Start()
        {
            // At this point the MessageReader and MessageWriter should be ready
            this.MessageReader = protocolChannel.MessageReader;
            this.MessageWriter = protocolChannel.MessageWriter;

            // Start the main message loop thread.  The Task is
            // not explicitly awaited because it is running on
            // an independent background thread.
            this.messageLoopThread = new AsyncContextThread("Message Dispatcher");
            this.messageLoopThread
                .Run(() => this.ListenForMessages(this.messageLoopCancellationToken.Token))
                .ContinueWith(this.OnListenTaskCompleted);
        }

        public void Stop()
        {
            // Stop the message loop thread
            if (this.messageLoopThread != null)
            {
                this.messageLoopCancellationToken.Cancel();
                this.messageLoopThread.Stop();
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        public void SetRequestHandler<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            Func<TParams, RequestContext<TResult>, Task> requestHandler)
        {
            this.SetRequestHandler(
                requestType,
                requestHandler,
                false);
        }

        public void SetRequestHandler<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            Func<TParams, RequestContext<TResult>, Task> requestHandler,
            bool overrideExisting)
        {
            if (overrideExisting)
            {
                // Remove the existing handler so a new one can be set
                this.requestHandlers.Remove(requestType.Method);
            }

            this.requestHandlers.Add(
                requestType.Method,
                (requestMessage, messageWriter) =>
                {
                    var requestContext =
                        new RequestContext<TResult>(
                            requestMessage,
                            messageWriter);

                    TParams typedParams = default(TParams);
                    if (requestMessage.Contents != null)
                    {
                        // TODO: Catch parse errors!
                        typedParams = requestMessage.Contents.ToObject<TParams>();
                    }

                    return requestHandler(typedParams, requestContext);
                });
        }

        public void SetEventHandler<TParams, TRegistrationOptions>(
            NotificationType<TParams, TRegistrationOptions> eventType,
            Func<TParams, EventContext, Task> eventHandler)
        {
            this.SetEventHandler(
                eventType,
                eventHandler,
                false);
        }

        public void SetEventHandler<TParams, TRegistrationOptions>(
            NotificationType<TParams, TRegistrationOptions> eventType,
            Func<TParams, EventContext, Task> eventHandler,
            bool overrideExisting)
        {
            if (overrideExisting)
            {
                // Remove the existing handler so a new one can be set
                this.eventHandlers.Remove(eventType.Method);
            }

            this.eventHandlers.Add(
                eventType.Method,
                (eventMessage, messageWriter) =>
                {
                    var eventContext = new EventContext(messageWriter);

                    TParams typedParams = default(TParams);
                    if (eventMessage.Contents != null)
                    {
                        // TODO: Catch parse errors!
                        typedParams = eventMessage.Contents.ToObject<TParams>();
                    }

                    return eventHandler(typedParams, eventContext);
                });
        }

        public void SetResponseHandler(Action<Message> responseHandler)
        {
            this.responseHandler = responseHandler;
        }

        #endregion

        #region Events

        public event EventHandler<Exception> UnhandledException;

        protected void OnUnhandledException(Exception unhandledException)
        {
            if (this.UnhandledException != null)
            {
                this.UnhandledException(this, unhandledException);
            }
        }

        #endregion

        #region Private Methods

        private async Task ListenForMessages(CancellationToken cancellationToken)
        {
            this.SynchronizationContext = SynchronizationContext.Current;

            // Run the message loop
            bool isRunning = true;
            while (isRunning && !cancellationToken.IsCancellationRequested)
            {
                Message newMessage = null;

                try
                {
                    // Read a message from the channel
                    newMessage = await this.MessageReader.ReadMessage();
                }
                catch (MessageParseException e)
                {
                    // TODO: Write an error response

                    Logger.Write(
                        LogLevel.Error,
                        "Could not parse a message that was received:\r\n\r\n" +
                        e.ToString());

                    // Continue the loop
                    continue;
                }
                catch (IOException e)
                {
                    // The stream has ended, end the message loop
                    Logger.Write(
                        LogLevel.Error,
                        string.Format(
                            "Stream terminated unexpectedly, ending MessageDispatcher loop\r\n\r\nException: {0}\r\n{1}",
                            e.GetType().Name,
                            e.Message));

                    break;
                }
                catch (ObjectDisposedException)
                {
                    Logger.Write(
                        LogLevel.Verbose,
                        "MessageReader attempted to read from a disposed stream, ending MessageDispatcher loop");

                    break;
                }
                catch (Exception e)
                {
                    Logger.Write(
                        LogLevel.Verbose,
                        "Caught unexpected exception '{0}' in MessageDispatcher loop:\r\n{1}",
                        e.GetType().Name,
                        e.Message);
                }

                // The message could be null if there was an error parsing the
                // previous message.  In this case, do not try to dispatch it.
                if (newMessage != null)
                {
                    // Process the message
                    await this.DispatchMessage(
                        newMessage,
                        this.MessageWriter);
                }
            }
        }

        protected async Task DispatchMessage(
            Message messageToDispatch,
            MessageWriter messageWriter)
        {
            Task handlerToAwait = null;

            if (messageToDispatch.MessageType == MessageType.Request)
            {
                Func<Message, MessageWriter, Task> requestHandler = null;
                if (this.requestHandlers.TryGetValue(messageToDispatch.Method, out requestHandler))
                {
                    handlerToAwait = requestHandler(messageToDispatch, messageWriter);
                }
                else
                {
                    // TODO: Message not supported error
                    Logger.Write(LogLevel.Error, $"MessageDispatcher: No handler registered for Request type '{messageToDispatch.Method}'");
                }
            }
            else if (messageToDispatch.MessageType == MessageType.Response)
            {
                if (this.responseHandler != null)
                {
                    this.responseHandler(messageToDispatch);
                }
            }
            else if (messageToDispatch.MessageType == MessageType.Event)
            {
                Func<Message, MessageWriter, Task> eventHandler = null;
                if (this.eventHandlers.TryGetValue(messageToDispatch.Method, out eventHandler))
                {
                    handlerToAwait = eventHandler(messageToDispatch, messageWriter);
                }
                else
                {
                    // TODO: Message not supported error
                    Logger.Write(LogLevel.Error, $"MessageDispatcher: No handler registered for Event type '{messageToDispatch.Method}'");
                }
            }
            else
            {
                // TODO: Return message not supported
                Logger.Write(LogLevel.Error, $"MessageDispatcher received unknown message type of method '{messageToDispatch.Method}'");
            }

            if (handlerToAwait != null)
            {
                try
                {
                    await handlerToAwait;
                }
                catch (TaskCanceledException)
                {
                    // Some tasks may be cancelled due to legitimate
                    // timeouts so don't let those exceptions go higher.
                }
                catch (AggregateException e)
                {
                    if (!(e.InnerExceptions[0] is TaskCanceledException))
                    {
                        // Cancelled tasks aren't a problem, so rethrow
                        // anything that isn't a TaskCanceledException
                        throw e;
                    }
                }
            }
        }

        private void OnListenTaskCompleted(Task listenTask)
        {
            if (listenTask.IsFaulted)
            {
                Logger.Write(
                    LogLevel.Error,
                    string.Format(
                        "MessageDispatcher loop terminated due to unhandled exception:\r\n\r\n{0}",
                        listenTask.Exception.ToString()));

                this.OnUnhandledException(listenTask.Exception);
            }
            else if (listenTask.IsCompleted || listenTask.IsCanceled)
            {
                // TODO: Dispose of anything?
            }
        }

        #endregion
    }
}
