//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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

        private const string _logCategory = "[JsonRpcQueue]";

        private int currentMessageId;
        private ChannelBase protocolChannel;
        private ProtocolEndpointState currentState;
        private IMessageDispatcher messageDispatcher;
        private AsyncContextThread messageQueueingThread;
        private AsyncContextThread messageDequeueingThread;
        private TaskCompletionSource<bool> endpointExitedTask;
        private SynchronizationContext originalSynchronizationContext;
        private CancellationTokenSource messageLoopCancellationToken =
            new CancellationTokenSource();

        private Dictionary<string, TaskCompletionSource<Message>> pendingRequests =
            new Dictionary<string, TaskCompletionSource<Message>>();

        private readonly ConcurrentQueue<QueuedMessage> _messageQueue = new ConcurrentQueue<QueuedMessage>();
        private readonly ConcurrentDictionary<string, QueuedMessage> _pendingCancelMessage = new ConcurrentDictionary<string, QueuedMessage>();

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
                this.StartListenAndQueueMessagesLoop();

                // Start loop to dequeue and dispatch messages
                this.StartDequeueAndDispatchMessagesLoop();

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

        private void StartListenAndQueueMessagesLoop()
        {
            // Start the main message loop thread.  The Task is
            // not explicitly awaited because it is running on
            // an independent background thread.
            this.messageQueueingThread = new AsyncContextThread("Message Listen and Queueing Loop");
            this.messageQueueingThread
                .Run(
                    () => this.ListenAndQueueMessagesAsync(this.messageLoopCancellationToken.Token),
                    this.Logger)
                .ContinueWith(t => {
                    this.OnListenAndQueueTaskCompleted(t);
                    this.Logger.Write(LogLevel.Verbose, $"{_logCategory} Message listen and queue loop stopped.");
                });
        }

        private void StartDequeueAndDispatchMessagesLoop()
        {
            // Start the main message loop thread.  The Task is
            // not explicitly awaited because it is running on
            // an independent background thread.
            this.messageDequeueingThread = new AsyncContextThread("Message Dequeueing and Dispatching Loop");
            this.messageDequeueingThread
                .Run(
                    () => this.DequeueAndDispatchMessagesAsync(this.messageLoopCancellationToken.Token),
                    this.Logger)
                .ContinueWith(_ => this.Logger.Write(LogLevel.Verbose, $"{_logCategory} Message dequeue and dispatch loop stopped."));
        }

        private void StopMessageLoop()
        {
            // Stop the message loop thread
            if (this.messageQueueingThread != null)
            {
                this.messageLoopCancellationToken.Cancel();
                this.messageQueueingThread.Stop();

                if (this.messageDequeueingThread != null)
                {
                    this.messageDequeueingThread.Stop();
                }

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

        private async Task ListenAndQueueMessagesAsync(CancellationToken cancellationToken)
        {
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
                catch (MessageParseException ex)
                {
                    // TODO: Write an error response
                    Logger.WriteHandledException($"{_logCategory} Could not parse a message that was received", ex);

                    // Continue the loop
                    continue;
                }
                catch (IOException ex)
                {
                    // The stream has ended, end the message loop
                    Logger.WriteException(
                        $"{_logCategory} Stream terminated unexpectedly, ending listen and queue messages loop",
                        ex);

                    break;
                }
                catch (ObjectDisposedException ex)
                {
                    // The stream has been disposed, end the message loop
                    Logger.WriteException(
                        $"{_logCategory} MessageReader attempted to read from a disposed stream, ending listen and queue messages loop",
                        ex);

                    break;
                }
                catch (Exception ex)
                {
                    // TODO: 2018-12-16 RKH It is curious that we forge ahead when an unexpected exception is encountered.
                    Logger.WriteHandledException(
                        $"{_logCategory} Caught exception in ProtocolEndpoint listen and queue messages loop",
                        ex);
                }

                // The message could be null if there was an error parsing the
                // previous message.  In this case, do not try to dispatch it.
                if (newMessage != null)
                {
                    QueuedMessage queuedMessage = new QueuedMessage(newMessage);

                    if (newMessage.Method == "$/cancelRequest")
                    {
                        // HACK: 2018-12-16 Should we have a class to represent the CancelRequest/Response?
                        // If current method is a $/cancelRequest, record that in our _pendingCancelMessage
                        // ConcurrentDictionary. Do not send a response, processed notifications messages are not supposed to.
                        int? id = newMessage.Contents?.Value<int>("id");
                        if (id.HasValue)
                        {
                            string idStr = id.Value.ToString();
                            _pendingCancelMessage[idStr] = queuedMessage;

                            Logger.Write(
                                LogLevel.Diagnostic,
                                $"{_logCategory} Pended message {newMessage.Method} for id:{idStr} at " +
                                $"{queuedMessage.QueueEntryTimeStr}, " +
                                $"seq:{queuedMessage.SequenceNumber} #pended:{_messageQueue.Count}");
                        }
                        else
                        {
                            Logger.Write(
                                LogLevel.Error,
                                $"{_logCategory} Failed to queue message {newMessage.Method} - could not find message id");
                        }
                    }
                    else
                    {
                        _messageQueue.Enqueue(queuedMessage);

                        Logger.Write(
                            LogLevel.Diagnostic,
                            $"{_logCategory} Queued message {newMessage.Method} id:{newMessage.Id} at " +
                            $"{queuedMessage.QueueEntryTimeStr}, " +
                            $"seq:{queuedMessage.SequenceNumber} #queued:{_messageQueue.Count}");
                    }
                }
            }
        }

        private void OnListenAndQueueTaskCompleted(Task task)
        {
            if (task.IsFaulted)
            {
                Logger.WriteHandledException($"{_logCategory} ProtocolEndpoint message loop terminated", task.Exception);
                this.OnUnhandledException(task.Exception);
            }
            else if (task.IsCompleted || task.IsCanceled)
            {
                // TODO: Dispose of anything?
            }
        }

        private async Task DequeueAndDispatchMessagesAsync(CancellationToken cancellationToken)
        {
            // Post event messages to this message loop.
            this.SynchronizationContext = SynchronizationContext.Current;

            // Run the message loop
            bool isRunning = true;
            while (isRunning && !cancellationToken.IsCancellationRequested)
            {
                QueuedMessage dequeuedMessage;
                string timeOnQueue = string.Empty;
                string queueName = string.Empty;

                try
                {
                    Logger.Write(LogLevel.Diagnostic, $"{_logCategory} Waiting for message to dequeue");

                    while (!_messageQueue.TryDequeue(out dequeuedMessage))
                    {
                        // Don't busy wait for a message to dequeue.
                        await Task.Delay(250);
                    }

                    // The message could be null if there was an error parsing the
                    // previous message.  In this case, do not try to dispatch it.
                    if (dequeuedMessage?.Message == null)
                    {
                        Logger.Write(
                            LogLevel.Warning,
                            $"{_logCategory} Dequeued empty message, seq:{dequeuedMessage.SequenceNumber}");
                    }
                    else
                    {
                        Message newMessage = dequeuedMessage.Message;

                        timeOnQueue = (DateTime.Now - dequeuedMessage.QueueEntryTime).TotalMilliseconds.ToString("F0");

                        // If a message has not been dispatched yet and it has a pending cancellation request,
                        // do not dispatch the message.  Simply return an error response to the client.
                        // TODO: 2018-12-16 RKH It is possible the LSP client sends the $/cancelRequest after this check
                        //       but before we have finished processing the message below which could leave an orphaned
                        //       cancel requeust in the queue.  There are various ways to handle that e.g. periodically
                        //       cleaning _pendingCancelMessage of messages of a certain age.
                        if ((newMessage.Id != null) && _pendingCancelMessage.TryRemove(newMessage.Id, out QueuedMessage queuedCancelMessage))
                        {
                            var responseError =
                                new ResponseError { Code = (int)MessageErrorCode.RequestCancelled, Message = "Request cancelled" };

                            var responseMessage =
                                Message.ResponseError(newMessage.Id, newMessage.Method, JToken.FromObject(responseError));

                            var timeOnCancelQueue = (DateTime.Now - queuedCancelMessage.QueueEntryTime).TotalMilliseconds.ToString("F0");

                            Logger.Write(
                                LogLevel.Diagnostic,
                                $"{_logCategory} Dequeued and cancelling messsage {newMessage.Method} id:{newMessage.Id}, " +
                                $"{queueName}queue wait time:{timeOnQueue}ms cancel queue wait time:{timeOnCancelQueue}ms " +
                                $"seq:{dequeuedMessage.SequenceNumber} cancel-seq:{queuedCancelMessage.SequenceNumber} " +
                                $"#queued:{_messageQueue.Count} #pending:{_pendingCancelMessage.Count}");

                            using (Logger.LogExecutionTime($"{_logCategory} Time to write cancellation response for seq:{dequeuedMessage.SequenceNumber}"))
                            {
                                await this.protocolChannel.MessageWriter.WriteMessageAsync(responseMessage);
                            }
                        }
                        else
                        {
                            if (newMessage.MessageType == MessageType.Response)
                            {
                                Logger.Write(
                                    LogLevel.Diagnostic,
                                    $"{_logCategory} Dequeued and handling response for messsage {newMessage.Method} id:{newMessage.Id}, " +
                                    $"{queueName}queue wait time:{timeOnQueue}ms " +
                                    $"seq:{dequeuedMessage.SequenceNumber} #queued:{_messageQueue.Count}");

                                using (Logger.LogExecutionTime($"{_logCategory} Time to handle response for {newMessage.Method} id:{newMessage.Id} seq:{dequeuedMessage.SequenceNumber}"))
                                {
                                    this.HandleResponse(newMessage);
                                }
                            }
                            else
                            {
                                Logger.Write(
                                    LogLevel.Diagnostic,
                                    $"{_logCategory} Dequeued and dispatching messsage {newMessage.Method} id:{newMessage.Id}, " +
                                    $"{queueName}queue wait time:{timeOnQueue}ms " +
                                    $"seq:{dequeuedMessage.SequenceNumber} #queued:{_messageQueue.Count}");

                                // Dispatch the message
                                using (Logger.LogExecutionTime($"{_logCategory} Time to dispatch {newMessage.Method} id:{newMessage.Id} seq:{dequeuedMessage.SequenceNumber}"))
                                {
                                    await this.messageDispatcher.DispatchMessageAsync(newMessage, this.protocolChannel.MessageWriter);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.WriteHandledException(
                        $"{_logCategory} Caught exception in ProtocolEndpoint dispatch and dequeue messages loop",
                        e);
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
