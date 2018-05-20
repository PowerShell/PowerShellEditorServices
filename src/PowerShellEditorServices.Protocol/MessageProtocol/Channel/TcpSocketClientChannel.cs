//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class TcpSocketClientChannel : ChannelBase
    {
        private IPsesLogger logger;
        private NetworkStream networkStream;

        public TcpSocketClientChannel(
            TcpClient tcpClient,
            IPsesLogger logger)
        {
            this.networkStream = tcpClient.GetStream();
            this.logger = logger;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.MessageReader =
                new MessageReader(
                    this.networkStream,
                    messageSerializer,
                    this.logger);

            this.MessageWriter =
                new MessageWriter(
                    this.networkStream,
                    messageSerializer,
                    this.logger);
        }

        protected override void Shutdown()
        {
            if (this.networkStream != null)
            {
                this.networkStream.Dispose();
                this.networkStream = null;
            }
        }

        public static async Task<TcpSocketClientChannel> Connect(
            int portNumber,
            MessageProtocolType messageProtocolType,
            IPsesLogger logger)
        {
            TcpClient tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, portNumber);

            var clientChannel = new TcpSocketClientChannel(tcpClient, logger);
            clientChannel.Start(messageProtocolType);

            return clientChannel;
        }
    }
}
