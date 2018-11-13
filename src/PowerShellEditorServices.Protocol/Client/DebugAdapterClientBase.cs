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
        private ILogger logger;
        private ProtocolEndpoint protocolEndpoint;
        private MessageDispatcher messageDispatcher;

        public DebugAdapterClient(ChannelBase clientChannel, ILogger logger)
        {
            this.logger = logger;
            this.messageDispatcher = new MessageDispatcher(logger);
            this.protocolEndpoint = new ProtocolEndpoint(
                clientChannel,
                messageDispatcher,
                logger);
        }

        public async Task StartAsync()
        {
            this.protocolEndpoint.Start();

            // Initialize the debug adapter
            await this.SendRequestAsync(
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

        public async Task LaunchScriptAsync(string scriptFilePath)
        {
            await this.SendRequestAsync(
                LaunchRequest.Type,
                new LaunchRequestArguments {
                    Script = scriptFilePath
                },
                true);

            await this.SendRequestAsync(
                ConfigurationDoneRequest.Type,
                null,
                true);
        }

        public Task SendEventAsync<TParams, TRegistrationOptions>(NotificationType<TParams, TRegistrationOptions> eventType, TParams eventParams)
        {
            return ((IMessageSender)protocolEndpoint).SendEventAsync(eventType, eventParams);
        }

        public Task<TResult> SendRequestAsync<TParams, TResult, TError, TRegistrationOptions>(RequestType<TParams, TResult, TError, TRegistrationOptions> requestType, TParams requestParams, bool waitForResponse)
        {
            return ((IMessageSender)protocolEndpoint).SendRequestAsync(requestType, requestParams, waitForResponse);
        }

        public Task<TResult> SendRequestAsync<TResult, TError, TRegistrationOptions>(RequestType0<TResult, TError, TRegistrationOptions> requestType0)
        {
            return ((IMessageSender)protocolEndpoint).SendRequestAsync(requestType0);
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

