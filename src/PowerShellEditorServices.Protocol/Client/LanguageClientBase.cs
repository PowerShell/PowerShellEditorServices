//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Client
{
    /// <summary>
    /// Provides a base implementation for language server clients.
    /// </summary>
    public abstract class LanguageClientBase : IMessageHandlers, IMessageSender
    {
        ILogger logger;
        private ProtocolEndpoint protocolEndpoint;
        private MessageDispatcher messageDispatcher;

        /// <summary>
        /// Initializes an instance of the language client using the
        /// specified channel for communication.
        /// </summary>
        /// <param name="clientChannel">The channel to use for communication with the server.</param>
        public LanguageClientBase(ChannelBase clientChannel, ILogger logger)
        {
            this.logger = logger;
            this.messageDispatcher = new MessageDispatcher(logger);
            this.protocolEndpoint = new ProtocolEndpoint(
                clientChannel,
                messageDispatcher,
                logger);
        }

        public Task Start()
        {
            this.protocolEndpoint.Start();

            // Initialize the implementation class
            return this.Initialize();
        }

        public async Task Stop()
        {
            await this.OnStop();

            // First, notify the language server that we're stopping
            var response =
                await this.SendRequest<object, object, object>(
                    ShutdownRequest.Type);

            await this.SendEvent(ExitNotification.Type, new object());

            this.protocolEndpoint.Stop();
        }

        protected virtual Task OnStop()
        {
            return Task.FromResult(true);
        }

        protected virtual Task Initialize()
        {
            return Task.FromResult(true);
        }

        public Task SendEvent<TParams, TRegistrationOptions>(NotificationType<TParams, TRegistrationOptions> eventType, TParams eventParams)
        {
            return ((IMessageSender)protocolEndpoint).SendEvent(eventType, eventParams);
        }

        public Task<TResult> SendRequest<TParams, TResult, TError, TRegistrationOptions>(RequestType<TParams, TResult, TError, TRegistrationOptions> requestType, TParams requestParams, bool waitForResponse)
        {
            return ((IMessageSender)protocolEndpoint).SendRequest(requestType, requestParams, waitForResponse);
        }

        public Task<TResult> SendRequest<TResult, TError, TRegistrationOptions>(RequestType0<TResult, TError, TRegistrationOptions> requestType0)
        {
            return ((IMessageSender)protocolEndpoint).SendRequest(requestType0);
        }

        public void SetRequestHandler<TParams, TResult, TError, TRegistrationOptions>(RequestType<TParams, TResult, TError, TRegistrationOptions> requestType, Func<TParams, RequestContext<TResult>, Task> requestHandler)
        {
            ((IMessageHandlers)messageDispatcher).SetRequestHandler(requestType, requestHandler);
        }

        public void SetRequestHandler<TResult, TError, TRegistrationOptions>(RequestType0<TResult, TError, TRegistrationOptions> requestType0, Func<RequestContext<TResult>, Task> requestHandler)
        {
            ((IMessageHandlers)messageDispatcher).SetRequestHandler(requestType0, requestHandler);
        }

        public void SetEventHandler<TParams, TRegistrationOptions>(NotificationType<TParams, TRegistrationOptions> eventType, Func<TParams, EventContext, Task> eventHandler)
        {
            ((IMessageHandlers)messageDispatcher).SetEventHandler(eventType, eventHandler);
        }
    }
}

