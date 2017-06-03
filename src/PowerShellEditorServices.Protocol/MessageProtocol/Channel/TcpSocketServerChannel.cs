//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class TcpSocketServerChannel : ChannelBase
    {
        private ILogger logger;
        private TcpClient tcpClient;
        private NetworkStream networkStream;

        public TcpSocketServerChannel(TcpClient tcpClient, ILogger logger)
        {
            this.tcpClient = tcpClient;
            this.networkStream = this.tcpClient.GetStream();
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

            if (this.tcpClient != null)
            {
                this.networkStream.Dispose();
#if CoreCLR
                this.tcpClient.Dispose();
#else
                this.tcpClient.Close();
#endif
                this.tcpClient = null;

                this.logger.Write(LogLevel.Verbose, "TCP client has been closed");
            }
        }
    }
}