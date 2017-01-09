//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Owin.WebSocket;

namespace Microsoft.PowerShell.EditorServices.Channel.WebSocket
{
    /// <summary>
    /// Implementation of <see cref="ChannelBase"/> that implements the streams necessary for 
    /// communicating via OWIN WebSockets. 
    /// </summary>
    public class WebSocketServerChannel : ChannelBase
    {
        private MemoryStream inStream;
        private readonly WebSocketConnection socketConnection;

        public WebSocketServerChannel(WebSocketConnection socket)
        {
            socketConnection = socket;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            inStream = new MemoryStream();

            // Set up the reader and writer
            this.MessageReader =
                new MessageReader(
                    this.inStream,
                    messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    new WebSocketStream(socketConnection), 
                    messageSerializer);
        }

        /// <summary>
        /// Dispatches data received during calls to OnMessageReceived in the <see cref="WebSocketConnection"/> class.
        /// </summary>
        /// <remarks>
        /// This method calls an overriden version of the <see cref="MessageDispatcher"/> that dispatches messages on 
        /// demand rather than running on a background thread. 
        /// </remarks>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task Dispatch(ArraySegment<byte> message)
        {
            //Clear our stream 
            inStream.SetLength(0);

            //Write data and dispatch to handlers
            await inStream.WriteAsync(message.ToArray(), 0, message.Count);
            inStream.Position = 0;
        }

        protected override void Shutdown()
        {
            this.socketConnection.Close(WebSocketCloseStatus.NormalClosure, "Server shutting down");
        }

        public override Task WaitForConnection()
        {
            // TODO: Need to update behavior here
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Overriden <see cref="MemoryStream"/> that sends data through a <see cref="WebSocketConnection"/> during the FlushAsync call. 
    /// </summary>
    /// <remarks>
    /// FlushAsync will send data via the SendBinary method of the <see cref="WebSocketConnection"/> class. The memory streams length will
    /// then be set to 0 to reset the stream for additional data to be written. 
    /// </remarks>
    internal class WebSocketStream : MemoryStream
    {
        private readonly WebSocketConnection _connection;

        public WebSocketStream(WebSocketConnection connection)
        {
            _connection = connection;
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            //Send to client socket and reset stream
            await _connection.SendBinary(new ArraySegment<byte>(ToArray()), true);
            SetLength(0);
        }
    }

    /// <summary>
    /// Base class for WebSocket connections that expose editor services.
    /// </summary>
    public abstract class EditorServiceWebSocketConnection : WebSocketConnection
    {
        protected EditorServiceWebSocketConnection() 
        {
            Channel = new WebSocketServerChannel(this);
        }

        protected ProtocolEndpoint Server { get; set; }

        protected WebSocketServerChannel Channel { get; private set; }

        public override void OnOpen()
        {
            Server.Start();
        }

        public override async Task OnMessageReceived(ArraySegment<byte> message, WebSocketMessageType type)
        {
            await Channel.Dispatch(message);
        }

        public override Task OnCloseAsync(WebSocketCloseStatus? closeStatus, string closeStatusDescription)
        {
            Server.Stop();

            return base.OnCloseAsync(closeStatus, closeStatusDescription);
        }
    }

    /// <summary>
    /// Web socket connections that expose the <see cref="LanguageServer"/>.
    /// </summary>
    public class LanguageServerWebSocketConnection : EditorServiceWebSocketConnection
    {
        public LanguageServerWebSocketConnection()
        {
            Server = new LanguageServer(null, null, Channel);
        }
    }

    /// <summary>
    /// Web socket connections that expose the <see cref="DebugAdapter"/>.
    /// </summary>
    public class DebugAdapterWebSocketConnection : EditorServiceWebSocketConnection
    {
        public DebugAdapterWebSocketConnection()
        {
            Server = new DebugAdapter(null, null, Channel, null);
        }
    }
}

