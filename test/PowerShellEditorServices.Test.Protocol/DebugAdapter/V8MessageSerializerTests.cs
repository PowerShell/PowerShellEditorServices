//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Serializers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Protocol.DebugAdapter
{
    public class TestMessageContents
    {
        public const string SomeFieldValue = "Some value";
        public const int NumberValue = 42;

        public string SomeField { get; set; }

        public int Number { get; set; }

        public TestMessageContents()
        {
            this.SomeField = SomeFieldValue;
            this.Number = NumberValue;
        }
    }

    public class V8MessageSerializerTests
    {
        private IMessageSerializer messageSerializer;

        private const string MessageId = "42";
        private const string MethodName = "testMethod";
        private static readonly JToken MessageContent = JToken.FromObject(new TestMessageContents());

        public V8MessageSerializerTests()
        {
            this.messageSerializer = new V8MessageSerializer();
        }

        [Fact]
        public void SerializesRequestMessages()
        {
            var messageObj =
                this.messageSerializer.SerializeMessage(
                    Message.Request(
                        MessageId, 
                        MethodName, 
                        MessageContent));

            AssertMessageFields(
                messageObj,
                checkSeq: true,
                checkCommand: true,
                checkParams: true);
        }

        [Fact]
        public void SerializesEventMessages()
        {
            var messageObj =
                this.messageSerializer.SerializeMessage(
                    Message.Event(
                        MethodName, 
                        MessageContent));

            AssertMessageFields(
                messageObj,
                checkEvent: true);
        }

        [Fact]
        public void SerializesResponseMessages()
        {
            var messageObj =
                this.messageSerializer.SerializeMessage(
                    Message.Response(
                        MessageId,
                        MethodName,
                        MessageContent));

            AssertMessageFields(
                messageObj,
                checkRequestSeq: true,
                checkCommand: true,
                checkResult: true);
        }

        [Fact]
        public void SerializesResponseWithErrorMessages()
        {
            var messageObj =
                this.messageSerializer.SerializeMessage(
                    Message.ResponseError(
                        MessageId,
                        MethodName,
                        MessageContent));

            AssertMessageFields(
                messageObj,
                checkRequestSeq: true,
                checkCommand: true,
                checkError: true);
        }

        private static void AssertMessageFields(
            JObject messageObj, 
            bool checkSeq = false,
            bool checkRequestSeq = false,
            bool checkCommand = false,
            bool checkEvent = false,
            bool checkParams = false,
            bool checkResult = false, 
            bool checkError = false)
        {
            JToken token = null;

            if (checkSeq)
            {
                Assert.True(messageObj.TryGetValue("seq", out token));
                Assert.Equal(MessageId, token.ToString());
            }
            else if (checkRequestSeq)
            {
                Assert.True(messageObj.TryGetValue("request_seq", out token));
                Assert.Equal(MessageId, token.ToString());
            }

            if (checkCommand)
            {
                Assert.True(messageObj.TryGetValue("command", out token));
                Assert.Equal(MethodName, token.ToString());
            }
            else if (checkEvent)
            {
                Assert.True(messageObj.TryGetValue("event", out token));
                Assert.Equal(MethodName, token.ToString());
            }

            if (checkError)
            {
                // TODO
            }
            else
            {
                string contentField = checkParams ? "arguments" : "body";
                Assert.True(messageObj.TryGetValue(contentField, out token));
                Assert.True(JToken.DeepEquals(token, MessageContent));
            }
        }
    }
}

