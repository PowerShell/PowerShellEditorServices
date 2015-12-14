using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    public class WebsocketClientChannel : ChannelBase
    {
        private string serviceProcessPath;
        private string serviceProcessArguments;
        private Process serviceProcess;

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
        /// <param name="serverProcessPath">The full path to the server process executable.</param>
        /// <param name="serverProcessArguments">Optional arguments to pass to the service process executable.</param>
        public WebsocketClientChannel(
            string serverProcessPath,
            params string[] serverProcessArguments)
        {
            this.serviceProcessPath = serverProcessPath;

            if (serverProcessArguments != null)
            {
                this.serviceProcessArguments =
                    string.Join(
                        " ",
                        serverProcessArguments);
            }

            this.serviceProcessArguments += " /websockets:9999";
        }

        protected override void Initialize(IMessageSerializer messageSerializer)
        {
            this.serviceProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = this.serviceProcessPath,
                    Arguments = this.serviceProcessArguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true,
            };

            // Start the process
            this.serviceProcess.Start();
            this.ProcessId = this.serviceProcess.Id;

            for (int retryCount = 0; retryCount < 5; retryCount++)
            {
                try
                {
                    this.socket = new ClientWebSocket();
                    this.socket.ConnectAsync(new Uri("ws://localhost:9999/ws"), CancellationToken.None).Wait();
                }
                catch (AggregateException ex)
                {
                    var wsException= ex.InnerExceptions.FirstOrDefault() as WebSocketException;
                    if (wsException != null)
                    {
                        Logger.Write(LogLevel.Warning,
                            string.Format("Failed to connect to WebSocket server. Retrying. {0} of 5. Error was '{1}'",
                                retryCount, wsException.Message));
                    }
                    else
                    {
                        throw;
                    }
                }
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

            this.serviceProcess.Kill();
        }
    }

    public class ClientWebSocketStream : MemoryStream
    {
        private readonly ClientWebSocket socket;

        public ClientWebSocketStream(ClientWebSocket socket)
        {
            this.socket = socket;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return socket.SendAsync(new ArraySegment<byte>(new byte[] {}), WebSocketMessageType.Binary, true, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.Run(() => socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), cancellationToken).Result.Count, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return socket.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, false,
                cancellationToken);
        }
    }
}
