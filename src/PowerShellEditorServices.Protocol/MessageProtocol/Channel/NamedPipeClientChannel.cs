//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class NamedPipeClientChannel : ChannelBase
    {
        private ILogger logger;
        private NamedPipeClientStream pipeClient;

        public NamedPipeClientChannel(
            NamedPipeClientStream pipeClient,
            ILogger logger)
        {
            this.pipeClient = pipeClient;
            this.logger = logger;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.MessageReader =
                new MessageReader(
                    this.pipeClient,
                    messageSerializer,
                    this.logger);

            this.MessageWriter =
                new MessageWriter(
                    this.pipeClient,
                    messageSerializer,
                    this.logger);
        }

        protected override void Shutdown()
        {
            if (this.pipeClient != null)
            {
                this.pipeClient.Dispose();
            }
        }

        public static async Task<NamedPipeClientChannel> Connect(
            string pipeName,
            MessageProtocolType messageProtocolType,
            ILogger logger)
        {
            var pipeClient =
                new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

            await pipeClient.ConnectAsync();

            var clientChannel = new NamedPipeClientChannel(pipeClient, logger);
            clientChannel.Start(messageProtocolType);

            return clientChannel;
        }
    }
}

