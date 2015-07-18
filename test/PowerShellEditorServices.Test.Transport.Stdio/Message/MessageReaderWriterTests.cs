// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Test.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System.IO;
using System.Reflection;
using Xunit;

namespace PSLanguageService.Test
{
    public class MessageReaderWriterTests
    {
        const string testEventString = "{\"event\":\"testEvent\",\"body\":null,\"seq\":0,\"type\":\"event\"}\r\n";
        const string testEventWithContentLengthString = "Content-Length: 56\r\n\r\n" + testEventString;

        private MessageTypeResolver messageTypeResolver;

        public MessageReaderWriterTests()
        {
            this.messageTypeResolver = new MessageTypeResolver();
            this.messageTypeResolver.ScanForMessageTypes(Assembly.GetExecutingAssembly());
        }

        [Fact]
        public void WritesMessageWithContentLength()
        {
            StringWriter stringWriter = new StringWriter();
            MessageWriter messageWriter = 
                new MessageWriter(
                    stringWriter,
                    MessageFormat.WithContentLength,
                    this.messageTypeResolver);

            messageWriter.WriteMessage(
                new TestEvent());

            string messageOutput = stringWriter.ToString();
            Assert.Equal(
                testEventWithContentLengthString,
                messageOutput);
        }

        [Fact]
        public void WritesMessageWithoutContentLength()
        {
            StringWriter stringWriter = new StringWriter();
            MessageWriter messageWriter = 
                new MessageWriter(
                    stringWriter, 
                    MessageFormat.WithoutContentLength,
                    this.messageTypeResolver);

            messageWriter.WriteMessage(
                new TestEvent());

            string messageOutput = stringWriter.ToString();
            Assert.Equal(
                testEventString,
                messageOutput);
        }

        [Fact]
        public void ReadsMessageWithContentLength()
        {
            MessageReader messageReader = 
                this.GetMessageReader(
                    testEventWithContentLengthString,
                    MessageFormat.WithContentLength);

            MessageBase messageResult = messageReader.ReadMessage().Result;
            TestEvent eventResult = Assert.IsType<TestEvent>(messageResult);
            Assert.Equal("testEvent", eventResult.EventType);
        }

        [Fact]
        public void ReadsMessageWithoutContentLength()
        {
            MessageReader messageReader =
                this.GetMessageReader(
                    testEventString,
                    MessageFormat.WithoutContentLength);

            MessageBase messageResult = messageReader.ReadMessage().Result;
            TestEvent eventResult = Assert.IsType<TestEvent>(messageResult);
            Assert.Equal("testEvent", eventResult.EventType);
        }

        private MessageReader GetMessageReader(
            string messageString,
            MessageFormat messageFormat)
        {
            return
                new MessageReader(
                    new StringReader(
                        messageString),
                    messageFormat,
                    this.messageTypeResolver);
        }
    }
}
