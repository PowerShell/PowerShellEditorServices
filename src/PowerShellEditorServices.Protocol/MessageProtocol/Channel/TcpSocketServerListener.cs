//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class TcpSocketServerListener : ServerListenerBase<TcpSocketServerChannel>
    {
        private int portNumber;
        private TcpListener tcpListener;

        public TcpSocketServerListener(
            MessageProtocolType messageProtocolType,
            int portNumber)
                : base(messageProtocolType)
        {
            this.portNumber = portNumber;
        }

        public override void Start()
        {
            if (this.tcpListener == null)
            {
                this.tcpListener = new TcpListener(IPAddress.Loopback, this.portNumber);
                this.tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                this.tcpListener.Start();
            }

            this.ListenForConnection();
        }

        public override void Stop()
        {
            if (this.tcpListener != null)
            {
                this.tcpListener.Stop();
                this.tcpListener = null;

                Logger.Write(LogLevel.Verbose, "TCP listener has been stopped");
            }
        }

        private void ListenForConnection()
        {
            Task.Factory.StartNew(
                async () =>
                {
                    try
                    {
                        TcpClient tcpClient = await this.tcpListener.AcceptTcpClientAsync();
                        this.OnClientConnect(
                            new TcpSocketServerChannel(
                                tcpClient));
                    }
                    catch (Exception e)
                    {
                        Logger.WriteException(
                            "An unhandled exception occurred while listening for a TCP client connection",
                            e);

                        throw e;
                    }
                });
        }
    }
}
