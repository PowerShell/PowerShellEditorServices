//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class TcpSocketClientChannel : ChannelBase
    {
        private NetworkStream networkStream;

        public TcpSocketClientChannel(TcpClient tcpClient)
        {
            this.networkStream = tcpClient.GetStream();
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.MessageReader =
                new MessageReader(
                    this.networkStream,
                    messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    this.networkStream,
                    messageSerializer);
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
            MessageProtocolType messageProtocolType)
        {
            TcpClient tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, portNumber);

            var clientChannel = new TcpSocketClientChannel(tcpClient);
            clientChannel.Start(messageProtocolType);

            return clientChannel;
        }
    }
}