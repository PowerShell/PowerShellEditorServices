using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.Practices.ServiceLocation;
using Owin;
using Owin.WebSocket;
using Owin.WebSocket.Extensions;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class WebsocketServerChannel : ChannelBase
    {
        private WebSocketStreamReaderWriter socketStreamReaderWriter;
        private WebSocketServiceLocator serviceLocator;
        private int port;
        private IDisposable selfHost;

        public WebsocketServerChannel(int port)
        {
            this.port = port;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            serviceLocator = new WebSocketServiceLocator();
            socketStreamReaderWriter = serviceLocator.GetInstance<WebSocketStreamReaderWriter>();
            ServiceLocator.SetLocatorProvider(() => serviceLocator);

            // Set up the reader and writer
            this.MessageReader =
                new MessageReader(
                    this.socketStreamReaderWriter.InStream,
                    messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    this.socketStreamReaderWriter.OutStream,
                    messageSerializer);

            this.selfHost = WebApp.Start<Startup>("http://localhost:" + port);
        }

        protected override void Shutdown()
        {
            this.socketStreamReaderWriter.Close(WebSocketCloseStatus.NormalClosure, "Server shutting down");

            if (this.selfHost != null)
                this.selfHost.Dispose();
        }
    }

    public class WebSocketStream : MemoryStream
    {
        private readonly WebSocketConnection _connection;

        public WebSocketStream(WebSocketConnection connection)
        {
            _connection = connection;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Logger.Write(LogLevel.Verbose, string.Format("Writing {0} bytes...", buffer.Length));
            return _connection.Send(new ArraySegment<byte>(buffer, offset, count), false, WebSocketMessageType.Binary);
        }

        //public override Task FlushAsync(CancellationToken cancellationToken)
        //{
        //    Logger.Write(LogLevel.Verbose, "Flush...");
        //    return _connection.Send(new ArraySegment<byte>(new byte[] {}), true, WebSocketMessageType.Binary);
        //}
    }

    [WebSocketRoute("/ws")]
    public class WebSocketStreamReaderWriter : WebSocketConnection
    {
        public Stream InStream { get; private set; }
        public WebSocketStream OutStream { get; private set; }

        public WebSocketStreamReaderWriter()
        {
            InStream = new MemoryStream();
            OutStream = new WebSocketStream(this);
        }
        public override async Task OnMessageReceived(ArraySegment<byte> message, WebSocketMessageType type)
        {
            Logger.Write(LogLevel.Verbose, string.Format("Message of {0} bytes received...", message.Count()));
            await InStream.WriteAsync(message.ToArray(), 0, message.Count);
            InStream.Position = 0;
        }
    }

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.MapWebSocketRoute<WebSocketStreamReaderWriter>(ServiceLocator.Current);
        }
    }

    public class WebSocketServiceLocator : ServiceLocatorImplBase
    {
        private readonly WebSocketStreamReaderWriter socketStreamReaderWriter;

        public WebSocketServiceLocator()
        {
            this.socketStreamReaderWriter = new WebSocketStreamReaderWriter();
        }

        protected override object DoGetInstance(Type serviceType, string key)
        {
            if (serviceType == typeof (WebSocketStreamReaderWriter))
            {
                return socketStreamReaderWriter;
            }
            throw new NotImplementedException();
        }

        protected override IEnumerable<object> DoGetAllInstances(Type serviceType)
        {
            if (serviceType == typeof(WebSocketStreamReaderWriter))
            {
                yield return socketStreamReaderWriter;
            }
            throw new NotImplementedException();
        }
    }
}
