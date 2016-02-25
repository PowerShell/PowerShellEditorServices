//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public abstract class DebugAdapterBase : ProtocolEndpoint
    {
        public DebugAdapterBase(ChannelBase serverChannel)
            : base (serverChannel, MessageProtocolType.DebugAdapter)
        {
        }

        /// <summary>
        /// Overridden by the subclass to provide initialization
        /// logic after the server channel is started.
        /// </summary>
        protected abstract void Initialize();

        /// <summary>
        /// Can be overridden by the subclass to provide shutdown
        /// logic before the server exits.
        /// </summary>
        protected virtual void Shutdown()
        {
            // No default implementation yet.
        }

        protected override Task OnStart()
        {
            // Register handlers for server lifetime messages
            this.SetRequestHandler(InitializeRequest.Type, this.HandleInitializeRequest);

            // Initialize the implementation class
            this.Initialize();

            return Task.FromResult(true);
        }

        protected override Task OnStop()
        {
            this.Shutdown();

            return Task.FromResult(true);
        }

        private async Task HandleInitializeRequest(
            object shutdownParams,
            RequestContext<InitializeResponseBody> requestContext)
        {
            // Send the Initialized event first so that we get breakpoints
            await requestContext.SendEvent(
                InitializedEvent.Type,
                null);

            // Now send the Initialize response to continue setup
            await requestContext.SendResult(new InitializeResponseBody());
        }
    }
}

