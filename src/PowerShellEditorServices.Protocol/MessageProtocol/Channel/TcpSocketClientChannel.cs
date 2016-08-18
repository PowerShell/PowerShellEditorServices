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
        private int portNumber;
        private NetworkStream networkStream;
        private IMessageSerializer messageSerializer;

        public TcpSocketClientChannel(int portNumber)
        {
            this.portNumber = portNumber;
        }

        public override async Task WaitForConnection()
        {
            TcpClient tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, this.portNumber);
            this.networkStream = tcpClient.GetStream();

            this.MessageReader =
                new MessageReader(
                    this.networkStream,
                    this.messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    this.networkStream,
                    this.messageSerializer);

            this.IsConnected = true;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.messageSerializer = messageSerializer;
        }

        protected override void Shutdown()
        {
            if (this.networkStream != null)
            {
                this.networkStream.Dispose();
                this.networkStream = null;
            }
        }
    }
}