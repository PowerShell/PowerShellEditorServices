//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Server
{
    public abstract class LanguageServerBase : ProtocolEndpoint
    {
        private bool isStarted;
        private ChannelBase serverChannel;
        private TaskCompletionSource<bool> serverExitedTask;

        public LanguageServerBase(ChannelBase serverChannel) : 
            base(serverChannel, MessageProtocolType.LanguageServer)
        {
            this.serverChannel = serverChannel;
        }

        protected override Task OnStart()
        {
            // Register handlers for server lifetime messages
            this.SetRequestHandler(ShutdownRequest.Type, this.HandleShutdownRequest);
            this.SetEventHandler(ExitNotification.Type, this.HandleExitNotification);

            // Initialize the implementation class
            this.Initialize();

            return Task.FromResult(true);
        }

        protected override async Task OnStop()
        {
            await this.Shutdown();
        }

        /// <summary>
        /// Overridden by the subclass to provide initialization
        /// logic after the server channel is started.
        /// </summary>
        protected abstract void Initialize();

        /// <summary>
        /// Can be overridden by the subclass to provide shutdown
        /// logic before the server exits.  Subclasses do not need
        /// to invoke or return the value of the base implementation.
        /// </summary>
        protected virtual Task Shutdown()
        {
            // No default implementation yet.
            return Task.FromResult(true);
        }

        private async Task HandleShutdownRequest(
            object shutdownParams,
            RequestContext<object> requestContext)
        {
            // Allow the implementor to shut down gracefully
            await this.Shutdown();

            await requestContext.SendResult(new object());
        }

        private async Task HandleExitNotification(
            object exitParams,
            EventContext eventContext)
        {
            // Stop the server channel
            await this.Stop();

            // Notify any waiter that the server has exited
            if (this.serverExitedTask != null)
            {
                this.serverExitedTask.SetResult(true);
            }
        }
    }
}

