//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Utility;
using System.Threading.Tasks;
using System;

namespace Microsoft.PowerShell.EditorServices.Protocol.Client
{
    public class DebugAdapterClient : IMessageSender, IMessageHandlers
    {
        private IPsesLogger logger;
        private ProtocolEndpoint protocolEndpoint;
        private MessageDispatcher messageDispatcher;

        public DebugAdapterClient(ChannelBase clientChannel, IPsesLogger logger)
        {
            this.logger = logger;
            this.messageDispatcher = new MessageDispatcher(logger);
            this.protocolEndpoint = new ProtocolEndpoint(
                clientChannel,
                messageDispatcher,
                logger);
        }

        public async Task Start()
        {
            this.protocolEndpoint.Start();

            // Initialize the debug adapter
            await this.SendRequest(
                InitializeRequest.Type,
                new InitializeRequestArguments
                {
                    LinesStartAt1 = true,
                    ColumnsStartAt1 = true
                },
                true);
        }

        public void Stop()
        {
            this.protocolEndpoint.Stop();
        }

        public async Task LaunchScript(string scriptFilePath)
        {
            await this.SendRequest(
                LaunchRequest.Type,
                new LaunchRequestArguments {
                    Script = scriptFilePath
                },
                true);

            await this.SendRequest(
                ConfigurationDoneRequest.Type,
                null,
                true);
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

