//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.Client
{
    public class DebugAdapterClient : ProtocolClient
    {
        public DebugAdapterClient(ChannelBase clientChannel)
            : base(clientChannel, MessageProtocolType.DebugAdapter)
        {
        }

        public Task LaunchScript(string scriptFilePath)
        {
            return this.SendRequest(
                LaunchRequest.Type,
                new LaunchRequestArguments
                {
                    Program = scriptFilePath
                });
        }

        protected override async Task OnStart()
        {
            // Initialize the debug adapter
            await this.SendRequest(
                InitializeRequest.Type,
                new InitializeRequestArguments
                {
                    LinesStartAt1 = true
                });
        }
    }
}

