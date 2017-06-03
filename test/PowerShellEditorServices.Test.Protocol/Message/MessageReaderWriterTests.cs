//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Serializers;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Test.Protocol.MessageProtocol
{
    public class MessageReaderWriterTests
    {
        const string TestEventString = "{\"type\":\"event\",\"event\":\"testEvent\",\"body\":null}";
        const string TestEventFormatString = "{{\"event\":\"testEvent\",\"body\":{{\"someString\":\"{0}\"}},\"seq\":0,\"type\":\"event\"}}";
        readonly int ExpectedMessageByteCount = Encoding.UTF8.GetByteCount(TestEventString);

        private ILogger logger;
        private IMessageSerializer messageSerializer;

        public MessageReaderWriterTests()
        {
            this.logger = new NullLogger();
            this.messageSerializer = new V8MessageSerializer();
        }

        [Fact]
        public async Task WritesMessage()
        {
            MemoryStream outputStream = new MemoryStream();

            MessageWriter messageWriter =
                new MessageWriter(
                    outputStream,
                    this.messageSerializer,
                    this.logger);

            // Write the message and then roll back the stream to be read
            // TODO: This will need to be redone!
            await messageWriter.WriteMessage(Message.Event("testEvent", null));
            outputStream.Seek(0, SeekOrigin.Begin);

            string expectedHeaderString =
                string.Format(
                    Constants.ContentLengthFormatString,
                    ExpectedMessageByteCount);

            byte[] buffer = new byte[128];
            await outputStream.ReadAsync(buffer, 0, expectedHeaderString.Length);

            Assert.Equal(
                expectedHeaderString,
                Encoding.ASCII.GetString(buffer, 0, expectedHeaderString.Length));

            // Read the message
            await outputStream.ReadAsync(buffer, 0, ExpectedMessageByteCount);

            Assert.Equal(
                TestEventString,
                Encoding.UTF8.GetString(buffer, 0, ExpectedMessageByteCount));

            outputStream.Dispose();
        }

        [Fact]
        public void ReadsMessage()
        {
            MemoryStream inputStream = new MemoryStream();
            MessageReader messageReader =
                new MessageReader(
                    inputStream,
                    this.messageSerializer,
                    this.logger);

            // Write a message to the stream
            byte[] messageBuffer = this.GetMessageBytes(TestEventString);
            inputStream.Write(
                this.GetMessageBytes(TestEventString),
                0,
                messageBuffer.Length);

            inputStream.Flush();
            inputStream.Seek(0, SeekOrigin.Begin);

            Message messageResult = messageReader.ReadMessage().Result;
            Assert.Equal("testEvent", messageResult.Method);

            inputStream.Dispose();
        }

        [Fact]
        public void ReadsManyBufferedMessages()
        {
            MemoryStream inputStream = new MemoryStream();
            MessageReader messageReader =
                new MessageReader(
                    inputStream,
                    this.messageSerializer,
                    this.logger);

            // Get a message to use for writing to the stream
            byte[] messageBuffer = this.GetMessageBytes(TestEventString);

            // How many messages of this size should we write to overflow the buffer?
            int overflowMessageCount =
                (int)Math.Ceiling(
                    (MessageReader.DefaultBufferSize * 1.5) / messageBuffer.Length);

            // Write the necessary number of messages to the stream
            for (int i = 0; i < overflowMessageCount; i++)
            {
                inputStream.Write(messageBuffer, 0, messageBuffer.Length);
            }

            inputStream.Flush();
            inputStream.Seek(0, SeekOrigin.Begin);

            // Read the written messages from the stream
            for (int i = 0; i < overflowMessageCount; i++)
            {
                Message messageResult = messageReader.ReadMessage().Result;
                Assert.Equal("testEvent", messageResult.Method);
            }

            inputStream.Dispose();
        }

        [Fact]
        public void ReaderResizesBufferForLargeMessages()
        {
            MemoryStream inputStream = new MemoryStream();
            MessageReader messageReader =
                new MessageReader(
                    inputStream,
                    this.messageSerializer,
                    this.logger);

            // Get a message with content so large that the buffer will need
            // to be resized to fit it all.
            byte[] messageBuffer =
                this.GetMessageBytes(
                    string.Format(
                        TestEventFormatString,
                        new String('X', (int)(MessageReader.DefaultBufferSize * 3))));

            inputStream.Write(messageBuffer, 0, messageBuffer.Length);
            inputStream.Flush();
            inputStream.Seek(0, SeekOrigin.Begin);

            Message messageResult = messageReader.ReadMessage().Result;
            Assert.Equal("testEvent", messageResult.Method);

            inputStream.Dispose();
        }

        private byte[] GetMessageBytes(string messageString, Encoding encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            byte[] messageBytes = Encoding.UTF8.GetBytes(messageString);
            byte[] headerBytes =
                Encoding.ASCII.GetBytes(
                    string.Format(
                        Constants.ContentLengthFormatString,
                        messageBytes.Length));

            // Copy the bytes into a single buffer
            byte[] finalBytes = new byte[headerBytes.Length + messageBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, finalBytes, 0, headerBytes.Length);
            Buffer.BlockCopy(messageBytes, 0, finalBytes, headerBytes.Length, messageBytes.Length);

            return finalBytes;
        }
    }
}

