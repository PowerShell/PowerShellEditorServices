//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.Client
{
    public class DebugAdapterClient : ProtocolEndpoint
    {
        public DebugAdapterClient(ChannelBase clientChannel, ILogger logger)
            : base(
                clientChannel,
                new MessageDispatcher(logger),
                MessageProtocolType.DebugAdapter,
                logger)
        {
        }

        public async Task LaunchScript(string scriptFilePath)
        {
            await this.SendRequest(
                LaunchRequest.Type,
                new LaunchRequestArguments {
                    Script = scriptFilePath
                });

            await this.SendRequest(ConfigurationDoneRequest.Type, null);
        }

        protected override Task OnStart()
        {
            // Initialize the debug adapter
            return this.SendRequest(
                InitializeRequest.Type,
                new InitializeRequestArguments
                {
                    LinesStartAt1 = true,
                    ColumnsStartAt1 = true
                });
        }
    }
}

