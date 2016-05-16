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
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Channel.WebSocket
{
    /// <summary>
    /// Implementation of <see cref="ChannelBase"/> that enables WebSocket communication.
    /// </summary>
    public class WebsocketClientChannel : ChannelBase
    {
        private readonly string serverUrl;
        private ClientWebSocket socket;
        private ClientWebSocketStream inputStream;
        private ClientWebSocketStream outputStream;

        /// <summary>
        /// Gets the process ID of the server process.
        /// </summary>
        public int ProcessId { get; private set; }

        /// <summary>
        /// Initializes an instance of the WebsocketClientChannel.
        /// </summary>
        /// <param name="url">The full path to the server process executable.</param>
        public WebsocketClientChannel(string url)
        {
            this.serverUrl = url;
        }

        public override async Task WaitForConnection()
        {
            try
            {
                await this.socket.ConnectAsync(new Uri(serverUrl), CancellationToken.None);
            }
            catch (AggregateException ex)
            {
                var wsException= ex.InnerExceptions.FirstOrDefault() as WebSocketException;
                if (wsException != null)
                {
                    Logger.Write(LogLevel.Warning,
                        string.Format("Failed to connect to WebSocket server. Error was '{0}'", wsException.Message));
                   
                }

                throw;
            }

            this.IsConnected = true;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.socket = new ClientWebSocket();
            this.inputStream = new ClientWebSocketStream(socket);
            this.outputStream = new ClientWebSocketStream(socket);

            // Set up the message reader and writer
            this.MessageReader =
                new MessageReader(
                    this.inputStream,
                    messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    this.outputStream,
                    messageSerializer);
        }

        protected override void Shutdown()
        {
            if (this.MessageReader != null)
            {
                this.MessageReader = null;
            }

            if (this.MessageWriter != null)
            {
                this.MessageWriter = null;
            }

            if (this.socket != null)
            {
                socket.Dispose();
            }
        }
    }

    /// <summary>
    /// Extension of <see cref="MemoryStream"/> that sends data to a WebSocket during FlushAsync 
    /// and reads during WriteAsync.
    /// </summary>
    internal class ClientWebSocketStream : MemoryStream
    {
        private readonly ClientWebSocket socket;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>
        /// It is expected that the socket is in an Open state. 
        /// </remarks>
        /// <param name="socket"></param>
        public ClientWebSocketStream(ClientWebSocket socket)
        {
            this.socket = socket;
        }

        /// <summary>
        /// Reads from the WebSocket. 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (socket.State != WebSocketState.Open)
            {
                return 0;
            }

            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken);
            } while (!result.EndOfMessage);
          
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                return 0;
            }

            return result.Count;
        }

        /// <summary>
        /// Sends the data in the stream to the buffer and clears the stream.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await socket.SendAsync(new ArraySegment<byte>(ToArray()), WebSocketMessageType.Binary, true, cancellationToken);
            SetLength(0);
        }
    }
}

