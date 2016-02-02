//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Client
{
    /// <summary>
    /// Provides a base implementation for language server clients.
    /// </summary>
    public abstract class LanguageClientBase : ProtocolEndpoint
    {
        /// <summary>
        /// Initializes an instance of the language client using the
        /// specified channel for communication.
        /// </summary>
        /// <param name="clientChannel">The channel to use for communication with the server.</param>
        public LanguageClientBase(ChannelBase clientChannel)
            : base(clientChannel, MessageProtocolType.LanguageServer)
        {
        }

        protected override Task OnStart()
        {
            // Initialize the implementation class
            return this.Initialize();
        }

        protected override async Task OnStop()
        {
            // First, notify the language server that we're stopping
            var response = await this.SendRequest(ShutdownRequest.Type, new object());
            await this.SendEvent(ExitNotification.Type, new object());
        }

        protected abstract Task Initialize();
    }
}

