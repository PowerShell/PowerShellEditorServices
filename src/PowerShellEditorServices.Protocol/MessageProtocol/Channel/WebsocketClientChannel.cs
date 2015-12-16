using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
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

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            try
            {
                this.socket = new ClientWebSocket();
                this.socket.ConnectAsync(new Uri(serverUrl), CancellationToken.None).Wait();
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
        }
    }

    public class ClientWebSocketStream : MemoryStream
    {
        private readonly ClientWebSocket socket;

        public ClientWebSocketStream(ClientWebSocket socket)
        {
            this.socket = socket;
        }

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

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await socket.SendAsync(new ArraySegment<byte>(ToArray()), WebSocketMessageType.Binary, true, cancellationToken);
            SetLength(0);
        }
    }
}
