using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Microsoft.PowerShell.EditorServices.Utility;
using Microsoft.Practices.ServiceLocation;
using Nito.AsyncEx;
using Owin;
using Owin.WebSocket;
using Owin.WebSocket.Extensions;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class WebsocketServerChannel : ChannelBase
    {
        private readonly WebSocketStreamReaderWriter socketStreamReaderWriter;

        public WebsocketServerChannel(WebSocketStreamReaderWriter socket)
        {
            socketStreamReaderWriter = socket;
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            // Set up the reader and writer
            this.MessageReader =
                new MessageReader(
                    this.socketStreamReaderWriter.InStream,
                    messageSerializer);

            this.MessageWriter =
                new MessageWriter(
                    this.socketStreamReaderWriter.OutStream,
                    messageSerializer);
        }

        protected override void Shutdown()
        {
            this.socketStreamReaderWriter.Close(WebSocketCloseStatus.NormalClosure, "Server shutting down");
        }
    }

    public class WebSocketStream : MemoryStream
    {
        private readonly WebSocketConnection _connection;
        private AsyncAutoResetEvent bufferLock = new AsyncAutoResetEvent(); 
        private AsyncReaderWriterLock asyncReaderWriterLock = new AsyncReaderWriterLock();

        private ConcurrentQueue<byte> byteQueue;

        public WebSocketStream(WebSocketConnection connection)
        {
            _connection = connection;
            byteQueue = new ConcurrentQueue<byte>();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await _connection.SendBinary(new ArraySegment<byte>(ToArray()), true);
            SetLength(0);
        }

        public override async Task<int> ReadAsync(byte[] outBuffer, int offset, int count, CancellationToken cancellationToken)
        {
            Logger.Write(LogLevel.Verbose, "ReadAsync...");
            int readCount = 0;
            try
            {
                await bufferLock.WaitAsync(cancellationToken).ConfigureAwait(false);

                Logger.Write(LogLevel.Verbose,
                    string.Format("Offset: {0} Count: {1} Queue Length: {2}", offset, count, byteQueue.Count));

                using (var releaser = await asyncReaderWriterLock.ReaderLockAsync().ConfigureAwait(false))
                {
                    for (int i = offset; i < count; i++)
                    {
                        byte b;
                        if (!byteQueue.TryDequeue(out b))
                        {
                            Logger.Write(LogLevel.Verbose, string.Format("Queue is empty: {0}", byteQueue.IsEmpty));
                            break;
                        }

                        outBuffer[i] = b;
                        readCount++;
                    }
                }

                Logger.Write(LogLevel.Verbose, string.Format("Read count: {0}", readCount));
            }
            catch (Exception ex)
            {
                Logger.Write(LogLevel.Error, ex.Message);
            }
            return readCount;
        }

        public async Task BufferData(byte[] data)
        {
            Logger.Write(LogLevel.Verbose, string.Format("Buffering data: {0}", data.Length));

            using (var releaser = await asyncReaderWriterLock.WriterLockAsync().ConfigureAwait(false))
            {
                foreach (var b in data)
                    byteQueue.Enqueue(b);       
            }

            bufferLock.Set();
        }
    }

    [WebSocketRoute("/ws")]
    public class WebSocketStreamReaderWriter : WebSocketConnection
    {
        public Guid ConnectionId = Guid.NewGuid();
        public WebSocketStream InStream { get; private set; }
        public WebSocketStream OutStream { get; private set; }

        private Server.LanguageServer languageServer;

        public WebSocketStreamReaderWriter()
        {
            Logger.Write(LogLevel.Verbose, "New websocket connection");
            InStream = new WebSocketStream(this);
            OutStream = new WebSocketStream(this);
        }

        public override async Task OnMessageReceived(ArraySegment<byte> message, WebSocketMessageType type)
        {
            Logger.Write(LogLevel.Verbose, string.Format("Message of {0} bytes received...", message.Count));

            try
            {
                await InStream.BufferData(message.ToArray());
                if (languageServer == null)
                {
                    languageServer = new Server.LanguageServer(new WebsocketServerChannel(this));
                    languageServer.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
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
            Logger.Initialize(Path.Combine(AssemblyDirectory, "Server.log"));
            app.MapWebSocketRoute<WebSocketStreamReaderWriter>();
        }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
    }
}
