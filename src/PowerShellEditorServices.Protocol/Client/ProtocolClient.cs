//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Client
{
    public class ProtocolClient
    {
        private bool isStarted;
        private int currentMessageId;
        private ChannelBase clientChannel;
        private MessageProtocolType messageProtocolType;
        private SynchronizationContext originalSynchronizationContext;

        private Dictionary<string, TaskCompletionSource<Message>> pendingRequests =
            new Dictionary<string, TaskCompletionSource<Message>>();

        /// <summary>
        /// Initializes an instance of the protocol client using the
        /// specified channel for communication.
        /// </summary>
        /// <param name="clientChannel">The channel to use for communication with the server.</param>
        /// <param name="messageProtocolType">The type of message protocol used by the server.</param>
        public ProtocolClient(
            ChannelBase clientChannel,
            MessageProtocolType messageProtocolType)
        {
            this.clientChannel = clientChannel;
            this.messageProtocolType = messageProtocolType;
        }

        /// <summary>
        /// Starts the language server client and sends the Initialize method.
        /// </summary>
        /// <returns>A Task that can be awaited for initialization to complete.</returns>
        public async Task Start()
        {
            if (!this.isStarted)
            {
                // Start the provided client channel
                this.clientChannel.Start(this.messageProtocolType);

                // Set the handler for any message responses that come back
                this.clientChannel.MessageDispatcher.SetResponseHandler(this.HandleResponse);

                // Listen for unhandled exceptions from the dispatcher
                this.clientChannel.MessageDispatcher.UnhandledException += MessageDispatcher_UnhandledException;

                // Notify implementation about client start
                await this.OnStart();

                // Client is now started
                this.isStarted = true;
            }
        }

        public async Task Stop()
        {
            if (this.isStarted)
            {
                // Stop the implementation first
                await this.OnStop();

                this.clientChannel.Stop();
                this.isStarted = false;
            }
        }

        /// <summary>
        /// Sends a request to the server
        /// </summary>
        /// <typeparam name="TParams"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="requestType"></param>
        /// <param name="requestParams"></param>
        /// <returns></returns>
        public Task<TResult> SendRequest<TParams, TResult>(
            RequestType<TParams, TResult> requestType, 
            TParams requestParams)
        {
            return this.SendRequest(requestType, requestParams, true);
        }

        public async Task<TResult> SendRequest<TParams, TResult>(
            RequestType<TParams, TResult> requestType, 
            TParams requestParams,
            bool waitForResponse)
        {
            this.currentMessageId++;

            TaskCompletionSource<Message> responseTask = null;

            if (waitForResponse)
            {
                responseTask = new TaskCompletionSource<Message>();
                this.pendingRequests.Add(
                    this.currentMessageId.ToString(), 
                    responseTask);
            }

            await this.clientChannel.MessageWriter.WriteRequest<TParams, TResult>(
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

        public async Task SendEvent<TParams>(EventType<TParams> eventType, TParams eventParams)
        {
            await this.clientChannel.MessageWriter.WriteMessage(
                Message.Event(
                    eventType.MethodName,
                    JToken.FromObject(eventParams)));
        }

        public void SetEventHandler<TParams>(
            EventType<TParams> eventType,
            Func<TParams, EventContext, Task> eventHandler)
        {
            this.clientChannel.MessageDispatcher.SetEventHandler(
                eventType,
                eventHandler,
                false);
        }

        public void SetEventHandler<TParams>(
            EventType<TParams> eventType,
            Func<TParams, EventContext, Task> eventHandler,
            bool overrideExisting)
        {
            this.clientChannel.MessageDispatcher.SetEventHandler(
                eventType,
                eventHandler,
                overrideExisting);
        }

        private void MessageDispatcher_UnhandledException(object sender, Exception e)
        {
            if (this.originalSynchronizationContext != null)
            {
                this.originalSynchronizationContext.Post(o => { throw e; }, null);
            }
        }

        private void HandleResponse(Message responseMessage)
        {
            TaskCompletionSource<Message> pendingRequestTask = null;

            if (this.pendingRequests.TryGetValue(responseMessage.Id, out pendingRequestTask))
            {
                pendingRequestTask.SetResult(responseMessage);
                this.pendingRequests.Remove(responseMessage.Id);
            }
        }

        protected virtual Task OnStart()
        {
            return Task.FromResult(true);
        }

        protected virtual Task OnStop()
        {
            return Task.FromResult(true);
        }
    }
}

