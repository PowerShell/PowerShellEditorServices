using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;
using Owin;
using Owin.WebSocket;
using Owin.WebSocket.Extensions;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class WebsocketServerChannel : ChannelBase
    {
        private readonly WebSocketStreamReaderWriter socketStreamReaderWriter;
        private readonly MemoryStream inStream;

        public WebSocketMessageDispatcher WebSocketMessageDispatcher { get; private set; }

        public WebsocketServerChannel(WebSocketStreamReaderWriter socket)
        {
            socketStreamReaderWriter = socket;
            inStream = new MemoryStream();
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            // Set up the reader and writer
            this.MessageReader =
                new MessageReader(
                    this.inStream,
                    messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    new WebSocketStream(socketStreamReaderWriter), 
                    messageSerializer);

            WebSocketMessageDispatcher = new WebSocketMessageDispatcher(MessageReader, MessageWriter);
            this.MessageDispatcher = WebSocketMessageDispatcher;
        }

        public async Task Dispatch(ArraySegment<byte> message)
        {
            inStream.SetLength(0);
            await inStream.WriteAsync(message.ToArray(), 0, message.Count);
            inStream.Position = 0;
            await WebSocketMessageDispatcher.DispatchMessage();
        }

        protected override void Shutdown()
        {
            this.socketStreamReaderWriter.Close(WebSocketCloseStatus.NormalClosure, "Server shutting down");
        }
    }

    public class WebSocketStream : MemoryStream
    {
        private readonly WebSocketConnection _connection;

        public WebSocketStream(WebSocketConnection connection)
        {
            _connection = connection;
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _connection.SendBinary(new ArraySegment<byte>(ToArray()), true);
            SetLength(0);
        }
    }

    [WebSocketRoute("/ws")]
    public class WebSocketStreamReaderWriter : WebSocketConnection
    {
        private WebsocketServerChannel _channel;
        private Server.LanguageServer languageServer;

        public override void OnOpen()
        {
            _channel = new WebsocketServerChannel(this);
            languageServer = new Server.LanguageServer(_channel);
            languageServer.Start();
        }

        public override async Task OnMessageReceived(ArraySegment<byte> message, WebSocketMessageType type)
        {
            Logger.Write(LogLevel.Verbose, string.Format("Message of {0} bytes received...", message.Count));
            await _channel.Dispatch(message);
        }

        public override Task OnOpenAsync()
        {
            Logger.Write(LogLevel.Normal, "Opening WebSocket");
            return base.OnOpenAsync();
        }

        public override Task OnCloseAsync(WebSocketCloseStatus? closeStatus, string closeStatusDescription)
        {
            Logger.Write(LogLevel.Normal, "Closing websocket");

            return base.OnCloseAsync(closeStatus, closeStatusDescription);
        }

        public override void OnReceiveError(Exception error)
        {
            Logger.Write(LogLevel.Error, "Error on web socket: " + error.Message);

            base.OnReceiveError(error);
        }
    }

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapWebSocketRoute<WebSocketStreamReaderWriter>();
        }
    }

    public class WebSocketMessageDispatcher : MessageDispatcher
    {
        public WebSocketMessageDispatcher(MessageReader messageReader, MessageWriter messageWriter) : base(messageReader, messageWriter)
        {
        }

        public async Task DispatchMessage()
        {
            var message = await this.messageReader.ReadMessage();
            await base.DispatchMessage(message, this.messageWriter);
        }
    }
}
