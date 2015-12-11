//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public abstract class ProtocolServer
    {
        private bool isStarted;
        private ChannelBase serverChannel;
        private MessageProtocolType messageProtocolType;
        private TaskCompletionSource<bool> serverExitedTask;
        private SynchronizationContext originalSynchronizationContext;

        /// <summary>
        /// Initializes an instance of the protocol server using the
        /// specified channel for communication.
        /// </summary>
        /// <param name="serverChannel">The channel to use for communication with the client.</param>
        /// <param name="messageProtocolType">The type of message protocol used by the server.</param>
        public ProtocolServer(
            ChannelBase serverChannel, 
            MessageProtocolType messageProtocolType)
        {
            this.serverChannel = serverChannel;
            this.messageProtocolType = messageProtocolType;
            this.originalSynchronizationContext = SynchronizationContext.Current;
        }

        public void Start()
        {
            if (!this.isStarted)
            {
                // Start the provided server channel
                this.serverChannel.Start(this.messageProtocolType);

                // Listen for unhandled exceptions from the dispatcher
                this.serverChannel.MessageDispatcher.UnhandledException += MessageDispatcher_UnhandledException;

                // Notify implementation about server start
                this.OnStart();

                // Server is now started
                this.isStarted = true;
            }
        }

        public void WaitForExit()
        {
            this.serverExitedTask = new TaskCompletionSource<bool>();
            this.serverExitedTask.Task.Wait();
        }

        public void Stop()
        {
            if (this.isStarted)
            {
                // Stop the implementation first
                this.OnStop();

                this.serverChannel.Stop();
                this.serverExitedTask.SetResult(true);
                this.isStarted = false;
            }
        }

        public void SetRequestHandler<TParams, TResult>(
            RequestType<TParams, TResult> requestType,
            Func<TParams, RequestContext<TResult>, Task> requestHandler)
        {
            this.serverChannel.MessageDispatcher.SetRequestHandler(
                requestType,
                requestHandler);
        }

        public void SetEventHandler<TParams>(
            EventType<TParams> eventType,
            Func<TParams, EventContext, Task> eventHandler)
        {
            this.serverChannel.MessageDispatcher.SetEventHandler(
                eventType,
                eventHandler);
        }

        /// <summary>
        /// Sends an event to the channel's endpoint.
        /// </summary>
        /// <typeparam name="TParams">The event parameter type.</typeparam>
        /// <param name="eventType">The type of event being sent.</param>
        /// <param name="eventParams">The event parameters being sent.</param>
        /// <returns>A Task that tracks completion of the send operation.</returns>
        public Task SendEvent<TParams>(
            EventType<TParams> eventType, 
            TParams eventParams)
        {
            // In a server, some events could be raised from a different 
            // thread.  To ensure that messages are written serially,
            // dispatch the SendEvent call to the message loop thread.

            if (!this.serverChannel.MessageDispatcher.InMessageLoopThread)
            {
                this.serverChannel.MessageDispatcher.SynchronizationContext.Post(
                    async (obj) =>
                    {
                        await this.serverChannel.MessageWriter.WriteEvent(
                            eventType,
                            eventParams);
                    }, null);

                return Task.FromResult(true);
            }
            else
            {
                return this.serverChannel.MessageWriter.WriteEvent(
                    eventType,
                    eventParams);
            }
        }

        protected virtual void OnStart()
        {
        }

        protected virtual void OnStop()
        {
        }

        private void MessageDispatcher_UnhandledException(object sender, Exception e)
        {
            if (this.serverExitedTask != null)
            {
                this.serverExitedTask.SetException(e);
            }
            else if (this.originalSynchronizationContext != null)
            {
                this.originalSynchronizationContext.Post(o => { throw e; }, null);
            }
        }
    }
}

