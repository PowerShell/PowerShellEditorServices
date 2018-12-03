//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    /// <summary>
    /// Provides behavior for a client or server endpoint that
    /// communicates using the specified protocol.
    /// </summary>
    public class ProtocolEndpoint : IMessageSender
    {
        private enum ProtocolEndpointState
        {
            NotStarted,
            Started,
            Shutdown
        }

        private int currentMessageId;
        private ChannelBase protocolChannel;
        private ProtocolEndpointState currentState;
        private IMessageDispatcher messageDispatcher;
        private AsyncContextThread messageLoopThread;
        private TaskCompletionSource<bool> endpointExitedTask;
        private SynchronizationContext originalSynchronizationContext;
        private CancellationTokenSource messageLoopCancellationToken =
            new CancellationTokenSource();

        private Dictionary<string, TaskCompletionSource<Message>> pendingRequests =
            new Dictionary<string, TaskCompletionSource<Message>>();

        private AsyncQueue<QueuedMessage> _messageQueue = new AsyncQueue<QueuedMessage>();
        private AsyncQueue<QueuedMessage> _lowPriorityMessageQueue = new AsyncQueue<QueuedMessage>();
        private HashSet<string> _pendingCancelMessage = new HashSet<string>();

        public SynchronizationContext SynchronizationContext { get; private set; }

        private bool InMessageLoopThread
        {
            get
            {
                // We're in the same thread as the message loop if the
                // current synchronization context equals the one we
                // know.
                return SynchronizationContext.Current == this.SynchronizationContext;
            }
        }

        protected ILogger Logger { get; private set; }

        /// <summary>
        /// Initializes an instance of the protocol server using the
        /// specified channel for communication.
        /// </summary>
        /// <param name="protocolChannel">
        /// The channel to use for communication with the connected endpoint.
        /// </param>
        /// <param name="messageProtocolType">
        /// The type of message protocol used by the endpoint.
        /// </param>
        public ProtocolEndpoint(
            ChannelBase protocolChannel,
            IMessageDispatcher messageDispatcher,
            ILogger logger)
        {
            this.protocolChannel = protocolChannel;
            this.messageDispatcher = messageDispatcher;
            this.Logger = logger;

            this.originalSynchronizationContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Starts the language server client and sends the Initialize method.
        /// </summary>
        public void Start()
        {
            if (this.currentState == ProtocolEndpointState.NotStarted)
            {
                // Listen for unhandled exceptions from the message loop
                this.UnhandledException += MessageDispatcher_UnhandledException;

                // Start loop to listen for and queue received messages
                this.StartMessageListenerLoop();

                // Start loop to dequeue and dispatch messages
                this.StartMessageDispatchLoop();

                // Endpoint is now started
                this.currentState = ProtocolEndpointState.Started;
            }
        }

        public void WaitForExit()
        {
            this.endpointExitedTask = new TaskCompletionSource<bool>();
            this.endpointExitedTask.Task.Wait();
        }

        public void Stop()
        {
            if (this.currentState == ProtocolEndpointState.Started)
            {
                // Make sure no future calls try to stop the endpoint during shutdown
                this.currentState = ProtocolEndpointState.Shutdown;

                // Stop the message loop and channel
                this.StopMessageLoop();
                this.protocolChannel.Stop();

                // Notify anyone waiting for exit
                if (this.endpointExitedTask != null)
                {
                    this.endpointExitedTask.SetResult(true);
                }
            }
        }

        #region Message Sending

        public Task<TResult> SendRequestAsync<TResult, TError, TRegistrationOptions>(
            RequestType0<TResult, TError, TRegistrationOptions> requestType0)
        {
            return this.SendRequestAsync(
                RequestType<Object, TResult, TError, TRegistrationOptions>.ConvertToRequestType(requestType0),
                 null);
        }


        /// <summary>
        /// Sends a request to the server
        /// </summary>
        /// <typeparam name="TParams"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="requestType"></param>
        /// <param name="requestParams"></param>
        /// <returns></returns>
        public Task<TResult> SendRequestAsync<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            TParams requestParams)
        {
            return this.SendRequestAsync(requestType, requestParams, true);
        }

        public async Task<TResult> SendRequestAsync<TParams, TResult, TError, TRegistrationOptions>(
            RequestType<TParams, TResult, TError, TRegistrationOptions> requestType,
            TParams requestParams,
            bool waitForResponse)
        {
            // Some requests may still be in the SynchronizationContext queue
            // after the server stops so don't act on those requests because
            // the protocol channel will already be disposed
            if (this.currentState == ProtocolEndpointState.Shutdown)
            {
                return default(TResult);
            }

            this.currentMessageId++;

            TaskCompletionSource<Message> responseTask = null;

            if (waitForResponse)
            {
                responseTask = new TaskCompletionSource<Message>();
                this.pendingRequests.Add(
                    this.currentMessageId.ToString(),
                    responseTask);
            }

            await this.protocolChannel.MessageWriter.WriteRequestAsync<TParams, TResult, TError, TRegistrationOptions>(
                requestType,
                requestParams,
                this.currentMessageId);

            if (responseTask != null)
            {
                var responseMessage = await responseTask.Task;

                return
                    responseMessage.Contents != null ?
                        responseMessage.Contents.ToObject<TResult>() :
                        default(TResult);
            }
            else
            {
                // TODO: Better default value here?
                return default(TResult);
            }
        }

        /// <summary>
        /// Sends an event to the channel's endpoint.
        /// </summary>
        /// <typeparam name="TParams">The event parameter type.</typeparam>
        /// <param name="eventType">The type of event being sent.</param>
        /// <param name="eventParams">The event parameters being sent.</param>
        /// <returns>A Task that tracks completion of the send operation.</returns>
        public Task SendEventAsync<TParams, TRegistrationOptions>(
            NotificationType<TParams, TRegistrationOptions> eventType,
            TParams eventParams)
        {
            // Some requests may still be in the SynchronizationContext queue
            // after the server stops so don't act on those requests because
            // the protocol channel will already be disposed
            if (this.currentState == ProtocolEndpointState.Shutdown)
            {
                return Task.FromResult(true);
            }

            // Some events could be raised from a different thread.
            // To ensure that messages are written serially, dispatch
            // dispatch the SendEvent call to the message loop thread.

            if (!this.InMessageLoopThread)
            {
                TaskCompletionSource<bool> writeTask = new TaskCompletionSource<bool>();

                this.SynchronizationContext.Post(
                    async (obj) =>
                    {
                        await this.protocolChannel.MessageWriter.WriteEventAsync(
                            eventType,
                            eventParams);

                        writeTask.SetResult(true);
                    }, null);

                return writeTask.Task;
            }
            else
            {
                return this.protocolChannel.MessageWriter.WriteEventAsync(
                    eventType,
                    eventParams);
            }
        }

        #endregion

        #region Message Handling

        private void HandleResponse(Message responseMessage)
        {
            TaskCompletionSource<Message> pendingRequestTask = null;

            if (this.pendingRequests.TryGetValue(responseMessage.Id, out pendingRequestTask))
            {
                pendingRequestTask.SetResult(responseMessage);
                this.pendingRequests.Remove(responseMessage.Id);
            }
        }

        private void StartMessageListenerLoop()
        {
            // Start the main message loop thread.  The Task is
            // not explicitly awaited because it is running on
            // an independent background thread.
            this.messageLoopThread = new AsyncContextThread("Message Listener");
            this.messageLoopThread
                .Run(
                    () => this.ListenForMessagesAsync(this.messageLoopCancellationToken.Token),
                    this.Logger)
                .ContinueWith(this.OnListenTaskCompleted);
        }

        private void StartMessageDispatchLoop()
        {
            // Start the main message loop thread.  The Task is
            // not explicitly awaited because it is running on
            // an independent background thread.
            this.messageLoopThread = new AsyncContextThread("Message Dispatcher");
            this.messageLoopThread
                .Run(
                    () => this.DispatchMessages(this.messageLoopCancellationToken.Token),
                    this.Logger);
        }

        private void StopMessageLoop()
        {
            // Stop the message loop thread
            if (this.messageLoopThread != null)
            {
                this.messageLoopCancellationToken.Cancel();
                this.messageLoopThread.Stop();
                SynchronizationContext.SetSynchronizationContext(null);
            }
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

        #region Event Handlers

        private void MessageDispatcher_UnhandledException(object sender, Exception e)
        {
            if (this.endpointExitedTask != null)
            {
                this.endpointExitedTask.SetException(e);
            }
            else if (this.originalSynchronizationContext != null)
            {
                this.originalSynchronizationContext.Post(o => { throw e; }, null);
            }
        }

        #endregion

        #region Private Methods

        private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
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
                    newMessage = await this.protocolChannel.MessageReader.ReadMessageAsync();
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
                    Logger.WriteException(
                        "Caught unhandled exception in ProtocolEndpoint message listener loop",
                        e);
                }

                // The message could be null if there was an error parsing the
                // previous message.  In this case, do not try to dispatch it.
                if (newMessage != null)
                {
                    // HACK: Probably should have a class to represent the CancelRequest/Response
                    // If current method is a $/cancelRequest, record that in our _pendingCancelMessage
                    // hashset and discard msg (processed notifications messages are not supposed to send a response).
                    if (newMessage.Method == "$/cancelRequest")
                    {
                        int? id = newMessage.Contents?.Value<int>("id");
                        if (id.HasValue)
                        {
                            var targetMessageId = id.Value.ToString();
                            _pendingCancelMessage.Add(targetMessageId);

                            Logger.Write(
                                LogLevel.Diagnostic,
                                $"Cancel request received for message id {targetMessageId} {newMessage.Method} - #pending: {_pendingCancelMessage.Count}");
                        }
                        else
                        {
                            Logger.Write(
                                LogLevel.Error,
                                $"Cancel request failed to retrieve message id for {newMessage.Method}");
                        }
                    }
                    else
                    {
                        QueuedMessage queuedMessage = new QueuedMessage(newMessage);

                        if (newMessage.Method == "textDocument/codeLens" ||
                            newMessage.Method == "textDocument/codeAction" ||
                            newMessage.Method == "textDocument/documentSymbol")
                        {
                            await _lowPriorityMessageQueue.EnqueueAsync(queuedMessage);

                            Logger.Write(
                                LogLevel.Diagnostic,
                                $"Queued request (low priority) for message id {newMessage.Id} {newMessage.Method} at {queuedMessage.QueueEntryTimeStr} " +
                                $"seq: {queuedMessage.SequenceNumber} - #queued: {_lowPriorityMessageQueue.Count}");
                        }
                        else
                        {
                            await _messageQueue.EnqueueAsync(queuedMessage);

                            Logger.Write(
                                LogLevel.Diagnostic,
                                $"Queued request for message id {newMessage.Id} {newMessage.Method} at {queuedMessage.QueueEntryTimeStr} " +
                                $"seq: {queuedMessage.SequenceNumber} - #queued: {_messageQueue.Count}");
                        }
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
                        "ProtocolEndpoint message loop terminated due to unhandled exception:\r\n\r\n{0}",
                        listenTask.Exception.ToString()));

                this.OnUnhandledException(listenTask.Exception);
            }
            else if (listenTask.IsCompleted || listenTask.IsCanceled)
            {
                // TODO: Dispose of anything?
            }
        }

        private async Task DispatchMessages(CancellationToken cancellationToken)
        {
            this.SynchronizationContext = SynchronizationContext.Current;

            // Run the message loop
            bool isRunning = true;
            while (isRunning && !cancellationToken.IsCancellationRequested)
            {
                QueuedMessage dequeuedMessage = null;
                string timeOnQueue = string.Empty;

                try
                {
                    if (_messageQueue.IsEmpty && !_lowPriorityMessageQueue.IsEmpty)
                    {
                        dequeuedMessage = await _lowPriorityMessageQueue.DequeueAsync(cancellationToken);
                    }
                    else
                    {
                        dequeuedMessage = await _messageQueue.DequeueAsync(cancellationToken);
                    }

                    timeOnQueue = (DateTime.Now - dequeuedMessage.QueueEntryTime).TotalMilliseconds.ToString("N0");
                }
                catch (Exception e)
                {
                    Logger.WriteException(
                        "Caught unhandled exception in ProtocolEndpoint message dispatcher loop",
                        e);
                }

                // The message could be null if there was an error parsing the
                // previous message.  In this case, do not try to dispatch it.
                if ((dequeuedMessage != null) && (dequeuedMessage.Message != null))
                {
                    Message newMessage = dequeuedMessage.Message;

                    // If a message has not been dispatched yet and it has a pending cancellation request,
                    // do not dispatch the message.  Simply return an error response to the client.
                    if (_pendingCancelMessage.Contains(newMessage.Id))
                    {
                        _pendingCancelMessage.Remove(newMessage.Id);

                        var responseError =
                            new ResponseError { Code = (int)MessageErrorCode.RequestCancelled, Message = "Request cancelled" };

                        var responseMessage =
                            Message.ResponseError(newMessage.Id, newMessage.Method, JToken.FromObject(responseError));

                        await this.protocolChannel.MessageWriter.WriteMessageAsync(responseMessage);

                        Logger.Write(
                            LogLevel.Diagnostic,
                            $"Cancel request processed for message id {newMessage.Id} {newMessage.Method} queue time: {timeOnQueue} ms " +
                            $"seq: {dequeuedMessage.SequenceNumber} - #pending: {_pendingCancelMessage.Count}");
                    }
                    else
                    {
                        if (newMessage.MessageType == MessageType.Response)
                        {
                            this.HandleResponse(newMessage);
                        }
                        else
                        {
                            Logger.Write(
                                LogLevel.Diagnostic,
                                $"Dispatching request for message id {newMessage.Id} {newMessage.Method} queue time: {timeOnQueue} ms " +
                                $"seq: {dequeuedMessage.SequenceNumber} - #queued: {_messageQueue.Count}");

                            // Process the message
                            await this.messageDispatcher.DispatchMessageAsync(newMessage, this.protocolChannel.MessageWriter);
                        }

                        // We may have gotten a cancellationRequest while the message was being processed.
                        // Remove that cancel request from our pending hashset.
                        if (_pendingCancelMessage.Contains(newMessage.Id))
                        {
                            _pendingCancelMessage.Remove(newMessage.Id);

                            Logger.Write(
                                LogLevel.Diagnostic,
                                $"Cancel request abandoned for already handled message id {newMessage.Id} {newMessage.Method} " +
                                $"queue time: {timeOnQueue} ms seq: {dequeuedMessage.SequenceNumber} - #pending: {_pendingCancelMessage.Count}");
                        }
                    }
                }
            }
        }

        #endregion
    }

    internal class QueuedMessage
    {
        private static ulong s_counter = 1;

        public QueuedMessage(Message message)
        {
            this.QueueEntryTime = DateTime.Now;
            this.Message = message;
            this.SequenceNumber = s_counter++;
        }

        public Message Message { get; private set; }

        public DateTime QueueEntryTime { get; private set; }

        public string QueueEntryTimeStr
        {
            get { return QueueEntryTime.ToString("hh:mm:ss.fff"); }
        }

        public ulong SequenceNumber { get; private set; }

        public override string ToString()
        {
            return $"{Message.Id ?? "<null>"} {Message.Method ?? "<null>"} timestamp:{QueueEntryTimeStr} seq:{SequenceNumber}";
        }
    }
}
