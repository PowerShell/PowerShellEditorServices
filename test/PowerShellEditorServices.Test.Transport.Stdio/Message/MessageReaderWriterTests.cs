// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using System.IO;
using Xunit;

namespace PSLanguageService.Test
{
    public class MessageReaderWriterTests
    {
        const string testEventString = "{\"event\":\"test\",\"body\":null,\"seq\":0,\"type\":\"event\"}\r\n";
        const string testEventStringWithContentLength = "Content-Length: 51\r\n\r\n" + testEventString;

        [Fact]
        public void WritesMessageWithContentLength()
        {
            StringWriter stringWriter = new StringWriter();
            MessageWriter messageWriter = 
                new MessageWriter(
                    stringWriter,
                    MessageFormat.WithContentLength);

            messageWriter.WriteMessage(
                new TestEvent());

            string messageOutput = stringWriter.ToString();
            Assert.Equal(
                testEventStringWithContentLength,
                messageOutput);
        }

        [Fact]
        public void WritesMessageWithoutContentLength()
        {
            StringWriter stringWriter = new StringWriter();
            MessageWriter messageWriter = 
                new MessageWriter(
                    stringWriter, 
                    MessageFormat.WithoutContentLength);

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
                new MessageReader(
                    new StringReader(
                        testEventStringWithContentLength),
                    MessageFormat.WithContentLength);

            MessageBase messageResult = messageReader.ReadMessage().Result;
            TestEvent eventResult = Assert.IsType<TestEvent>(messageResult);
            Assert.Equal("test", eventResult.EventType);
        }

        [Fact]
        public void ReadsMessageWithoutContentLength()
        {
            MessageReader messageReader =
                new MessageReader(
                    new StringReader(
                        testEventString),
                    MessageFormat.WithoutContentLength);

            MessageBase messageResult = messageReader.ReadMessage().Result;
            TestEvent eventResult = Assert.IsType<TestEvent>(messageResult);
            Assert.Equal("test", eventResult.EventType);
        }
    }

    internal class TestEvent : EventBase<object>
    {
        public TestEvent()
        {
            this.EventType = "test";
        }
    }
}
