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

namespace PsesPsClient
{
    public class LspPipe : IDisposable
    {
        private static readonly IReadOnlyDictionary<string, Type> s_messageBodyTypes = new Dictionary<string, Type>()
        {

        };

        private readonly NamedPipeClientStream _namedPipeClient;

        private readonly StringBuilder _headerBuffer;

        private readonly JsonSerializerSettings _jsonSettings;

        private readonly JsonSerializer _jsonSerializer;

        private readonly JsonRpcMessageSerializer _jsonRpcSerializer;

        private readonly Encoding _pipeEncoding;

        private int _msgId;

        private StreamReader _reader;

        private StreamWriter _writer;

        private char[] _readerBuffer;

        public LspPipe(NamedPipeClientStream namedPipeClient)
        {
            _namedPipeClient = namedPipeClient;

            _readerBuffer = new char[1024];

            _headerBuffer = new StringBuilder(128);

            _jsonSettings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            _jsonSerializer = JsonSerializer.Create(_jsonSettings);

            _jsonRpcSerializer = new JsonRpcMessageSerializer();

            _pipeEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        public bool HasContent
        {
            get
            {
                return _reader.Peek() > 0;
            }
        }

        public void Connect()
        {
            _namedPipeClient.Connect(timeout: 1000);
            _reader = new StreamReader(_namedPipeClient, _pipeEncoding);
            _writer = new StreamWriter(_namedPipeClient, _pipeEncoding)
            {
                AutoFlush = true
            };
        }

        public void Write(
            string method,
            object parameters)
        {
            _msgId++;

            Message msg = Message.Request(
                _msgId.ToString(),
                method,
                JToken.FromObject(parameters, _jsonSerializer));

            JObject msgJson = _jsonRpcSerializer.SerializeMessage(msg);
            string msgString = JsonConvert.SerializeObject(msgJson, _jsonSettings);
            byte[] msgBytes = _pipeEncoding.GetBytes(msgString);

            string header = "Content-Length: " + msgBytes.Length + "\r\n\r\n";

            _writer.Write(header);
            _writer.Write(msgBytes);
        }

        public LspMessage Read()
        {
            int contentLength = GetContentLength();
            string msgString = ReadString(contentLength);
            JObject msgJson = JObject.Parse(msgString);

            if (!msgJson.TryGetValue("method", out JToken methodJsonToken)
                || !(methodJsonToken is JValue methodJsonValue)
                || !(methodJsonValue.Value is string method))
            {
                throw new Exception($"No method given on message: '{msgString}'");
            }

            if (!s_messageBodyTypes.TryGetValue(method, out Type bodyType))
            {
                throw new Exception($"Unknown message method: '{method}'");
            }

            int id = (int)msgJson["id"];

            object body = msgJson["params"].ToObject(bodyType);

            return new LspMessage(id, method, body);
        }

        public void Dispose()
        {
            _namedPipeClient.Dispose();
        }

        private string ReadString(int bytesToRead)
        {
            if (bytesToRead > _readerBuffer.Length)
            {
                Array.Resize(ref _readerBuffer, _readerBuffer.Length * 2);
            }

            _reader.Read(_readerBuffer, 0, bytesToRead);

            return new string(_readerBuffer);
        }

        private int GetContentLength()
        {
            _headerBuffer.Clear();
            int endHeaderState = 0;
            int currChar;
            while ((currChar = _reader.Read()) >= 0)
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
                        endHeaderState = 1;
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
    }

    public class LspMessage
    {
        public LspMessage(int id, string method, object body)
        {
            Id = id;
            Method = method;
            Body = body;
        }

        public int Id { get; }

        public string Method { get; }

        public object Body;
    }
}
