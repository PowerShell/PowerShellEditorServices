using System;
using System.IO.Pipes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Serializers;
using System.Text;
using System.IO;
using Newtonsoft.Json.Linq;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace PsesPsClient
{
    /// <summary>
    /// A Language Server Protocol named pipe connection.
    /// </summary>
    public class LspPipe : IDisposable
    {
        /// <summary>
        /// Create a new LSP pipe around a given named pipe.
        /// </summary>
        /// <param name="pipeName">The name of the named pipe to use.</param>
        /// <returns>A new LspPipe instance around the given named pipe.</returns>
        public static LspPipe Create(string pipeName)
        {
            var pipeClient = new NamedPipeClientStream(
                pipeName: pipeName,
                serverName: ".",
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);

            return new LspPipe(pipeClient);
        }

        private readonly NamedPipeClientStream _namedPipeClient;

        private readonly JsonSerializerSettings _jsonSettings;

        private readonly JsonSerializer _jsonSerializer;

        private readonly JsonRpcMessageSerializer _jsonRpcSerializer;

        private readonly Encoding _pipeEncoding;

        private int _msgId;

        private StreamWriter _writer;

        private MessageStreamListener _listener;

        /// <summary>
        /// Create a new LSP pipe around a named pipe client stream.
        /// </summary>
        /// <param name="namedPipeClient">The named pipe client stream to use for the LSP pipe.</param>
        public LspPipe(NamedPipeClientStream namedPipeClient)
        {
            _namedPipeClient = namedPipeClient;

            _jsonSettings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            _jsonSerializer = JsonSerializer.Create(_jsonSettings);

            _jsonRpcSerializer = new JsonRpcMessageSerializer();

            _pipeEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        /// <summary>
        /// Connect to the named pipe server.
        /// </summary>
        public void Connect()
        {
            _namedPipeClient.Connect(timeout: 1000);
            _listener = new MessageStreamListener(new StreamReader(_namedPipeClient, _pipeEncoding));
            _writer = new StreamWriter(_namedPipeClient, _pipeEncoding)
            {
                AutoFlush = true
            };

            _listener.Start();
        }

        /// <summary>
        /// Write a request to the LSP pipe.
        /// </summary>
        /// <param name="method">The method of the request.</param>
        /// <param name="parameters">The parameters of the request. May be null.</param>
        /// <returns>A representation of the request sent.</returns>
        public LspRequest WriteRequest(
            string method,
            object parameters)
        {
            _msgId++;

            Message msg = Message.Request(
                _msgId.ToString(),
                method,
                parameters != null ? JToken.FromObject(parameters, _jsonSerializer) : JValue.CreateNull());

            JObject msgJson = _jsonRpcSerializer.SerializeMessage(msg);
            string msgString = JsonConvert.SerializeObject(msgJson, _jsonSettings);
            byte[] msgBytes = _pipeEncoding.GetBytes(msgString);

            string header = "Content-Length: " + msgBytes.Length + "\r\n\r\n";

            _writer.Write(header + msgString);
            _writer.Flush();

            return new LspRequest(msg.Id, method, msgJson["params"]);
        }

        /// <summary>
        /// Get all the pending notifications from the server.
        /// </summary>
        /// <returns>Any pending notifications from the server.</returns>
        public IEnumerable<LspNotification> GetNotifications()
        {
            return _listener.DrainNotifications();
        }

        /// <summary>
        /// Get all the pending requests from the server.
        /// </summary>
        /// <returns>Any pending requests from the server.</returns>
        public IEnumerable<LspRequest> GetRequests()
        {
            return _listener.DrainRequests();
        }

        /// <summary>
        /// Get the next response from the server, if one is available within the given time.
        /// </summary>
        /// <param name="response">The next response from the server.</param>
        /// <param name="millisTimeout">How long to wait for a response.</param>
        /// <returns>True if there is a next response, false if it timed out.</returns>
        public bool TryGetNextResponse(out LspResponse response, int millisTimeout)
        {
            return _listener.TryGetNextResponse(out response, millisTimeout);
        }

        public void Dispose()
        {
            _namedPipeClient.Close();
            _writer.Dispose();
            _listener.Dispose();
            _namedPipeClient.Dispose();
        }
    }

    /// <summary>
    /// A dedicated listener to run a thread for receiving pipe messages,
    /// so the the pipe is not blocked.
    /// </summary>
    public class MessageStreamListener : IDisposable
    {
        private readonly StreamReader _stream;

        private readonly StringBuilder _headerBuffer;

        private readonly ConcurrentQueue<LspRequest> _requestQueue;

        private readonly ConcurrentQueue<LspNotification> _notificationQueue;

        private readonly BlockingCollection<LspResponse> _responseBlockingOutput;

        private char[] _readerBuffer;

        private readonly CancellationTokenSource _cancellationSource;

        /// <summary>
        /// Create a listener around a stream.
        /// </summary>
        /// <param name="stream">The stream to listen for messages on.</param>
        public MessageStreamListener(StreamReader stream)
        {
            _stream = stream;
            _readerBuffer = new char[1024];
            _headerBuffer = new StringBuilder(128);
            _notificationQueue = new ConcurrentQueue<LspNotification>();
            _requestQueue = new ConcurrentQueue<LspRequest>();
            _responseBlockingOutput = new BlockingCollection<LspResponse>();
            _cancellationSource = new CancellationTokenSource();
        }

        public IEnumerable<LspNotification> DrainNotifications()
        {
            return DrainQueue(_notificationQueue);
        }

        public IEnumerable<LspRequest> DrainRequests()
        {
            return DrainQueue(_requestQueue);
        }

        public bool TryGetNextResponse(out LspResponse response)
        {
            return _responseBlockingOutput.TryTake(out response);
        }

        public bool TryGetNextResponse(out LspResponse response, int millisTimeout)
        {
            return _responseBlockingOutput.TryTake(out response, millisTimeout);
        }

        public void Start()
        {
            Task.Run(() => RunListenLoop());
        }

        public void Stop()
        {
            _cancellationSource.Cancel();
        }

        public void Dispose()
        {
            Stop();
            _stream.Dispose();
        }

        private async Task RunListenLoop()
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                LspMessage msg = await ReadMessage().ConfigureAwait(false);
                switch (msg)
                {
                    case LspNotification notification:
                        _notificationQueue.Enqueue(notification);
                        continue;

                    case LspResponse response:
                        _responseBlockingOutput.Add(response);
                        continue;

                    case LspRequest request:
                        _requestQueue.Enqueue(request);
                        continue;
                }
            }
        }

        private async Task<LspMessage> ReadMessage()
        {
            int contentLength = GetContentLength();
            string msgString = await ReadString(contentLength).ConfigureAwait(false);
            JObject msgJson = JObject.Parse(msgString);

            if (msgJson.TryGetValue("method", out JToken methodToken))
            {
                string method = ((JValue)methodToken).Value.ToString();
                if (msgJson.TryGetValue("id", out JToken idToken))
                {
                    string requestId = ((JValue)idToken).Value.ToString();
                    return new LspRequest(requestId, method, msgJson["params"]);
                }

                return new LspNotification(method, msgJson["params"]);
            }

            string id = ((JValue)msgJson["id"]).Value.ToString();

            if (msgJson.TryGetValue("result", out JToken resultToken))
            {
                return new LspSuccessfulResponse(id, resultToken);
            }

            JObject errorBody = (JObject)msgJson["error"];
            JsonRpcErrorCode errorCode = (JsonRpcErrorCode)(int)((JValue)errorBody["code"]).Value;
            string message = (string)((JValue)errorBody["message"]).Value;
            return new LspErrorResponse(id, errorCode, message, errorBody["data"]);
        }

        private async Task<string> ReadString(int bytesToRead)
        {
            if (bytesToRead > _readerBuffer.Length)
            {
                Array.Resize(ref _readerBuffer, _readerBuffer.Length * 2);
            }

            int readLen = await _stream.ReadAsync(_readerBuffer, 0, bytesToRead).ConfigureAwait(false);

            return new string(_readerBuffer, 0, readLen);
        }

        private int GetContentLength()
        {
            _headerBuffer.Clear();
            int endHeaderState = 0;
            int currChar;
            while ((currChar = _stream.Read()) >= 0)
            {
                char c = (char)currChar;
                _headerBuffer.Append(c);
                switch (c)
                {
                    case '\r':
                        if (endHeaderState == 2)
                        {
                            endHeaderState = 3;
                            continue;
                        }

                        if (endHeaderState == 0)
                        {
                            endHeaderState = 1;
                            continue;
                        }

                        endHeaderState = 0;
                        continue;

                    case '\n':
                        if (endHeaderState == 1)
                        {
                            endHeaderState = 2;
                            continue;
                        }

                        // This is the end, my only friend, the end
                        if (endHeaderState == 3)
                        {
                            return ParseContentLength(_headerBuffer.ToString());
                        }

                        endHeaderState = 0;
                        continue;

                    default:
                        endHeaderState = 0;
                        continue;
                }
            }

            throw new Exception("Buffer emptied before end of headers");
        }

        private static int ParseContentLength(string headers)
        {
            const string clHeaderPrefix = "Content-Length: ";

            int clIdx = headers.IndexOf(clHeaderPrefix);
            if (clIdx < 0)
            {
                throw new Exception("No Content-Length header found");
            }

            int endIdx = headers.IndexOf("\r\n", clIdx);
            if (endIdx < 0)
            {
                throw new Exception("Header CRLF terminator not found");
            }

            int numStartIdx = clIdx + clHeaderPrefix.Length;
            int numLength = endIdx - numStartIdx;

            return int.Parse(headers.Substring(numStartIdx, numLength));
        }

        private static IEnumerable<TElement> DrainQueue<TElement>(ConcurrentQueue<TElement> queue)
        {
            if (queue.IsEmpty)
            {
                return Enumerable.Empty<TElement>();
            }

            var list = new List<TElement>();
            while (queue.TryDequeue(out TElement element))
            {
                list.Add(element);
            }
            return list;
        }

    }

    public abstract class LspMessage
    {
        protected LspMessage()
        {
        }
    }

    public class LspNotification : LspMessage
    {
        public LspNotification(string method, JToken parameters)
        {
            Method = method;
            Params = parameters;
        }

        public string Method { get; }

        public JToken Params { get; }
    }

    public class LspRequest : LspMessage
    {
        public LspRequest(string id, string method, JToken parameters)
        {
            Id = id;
            Method = method;
            Params = parameters;
        }

        public string Id { get; }

        public string Method { get; }

        public JToken Params { get; }
    }

    public abstract class LspResponse : LspMessage
    {
        protected LspResponse(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }

    public class LspSuccessfulResponse : LspResponse
    {
        public LspSuccessfulResponse(string id, JToken result)
            : base(id)
        {
            Result = result;
        }

        public JToken Result { get; }
    }

    public class LspErrorResponse : LspResponse
    {
        public LspErrorResponse(
            string id,
            JsonRpcErrorCode code,
            string message,
            JToken data)
                : base(id)
        {
            Code = code;
            Message = message;
            Data = data;
        }

        public JsonRpcErrorCode Code { get; }

        public string Message { get; }

        public JToken Data { get; }
    }

    public enum JsonRpcErrorCode : int
    {
        ParseError = -32700,
        InvalidRequest = -32600,
        MethodNotFound = -32601,
        InvalidParams = -32602,
        InternalError = -32603,
        ServerErrorStart = -32099,
        ServerErrorEnd = -32000,
        ServerNotInitialized = -32002,
        UnknownErrorCode = -32001,
        RequestCancelled = -32800,
        ContentModified = -32801,
    }
}
