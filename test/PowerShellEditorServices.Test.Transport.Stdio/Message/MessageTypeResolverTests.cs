//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System;
using System.Reflection;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Transport.Stdio.Message
{
    public class MessageTypeResolverTests
    {
        private MessageTypeResolver messageTypeResolver;

        public MessageTypeResolverTests()
        {
            // Load message types in the current assembly
            this.messageTypeResolver = new MessageTypeResolver();
            this.messageTypeResolver.ScanForMessageTypes(
                Assembly.GetExecutingAssembly());
        }

        [Fact]
        public void MessageTypeResolverFindsRequestTypeByName()
        {
            this.FindMessageTypeByName<TestRequest>(
                MessageType.Request,
                "testRequest");
        }

        [Fact]
        public void MessageTypeResolverFindsRequestTypeNameByType()
        {
            this.FindMessageTypeNameByType<TestRequest>(
                "testRequest");
        }

        [Fact]
        public void MessageTypeResolverFindsResponseTypeByName()
        {
            this.FindMessageTypeByName<TestResponse>(
                MessageType.Response,
                "testResponse");
        }

        [Fact]
        public void MessageTypeResolverFindsResponseTypeNameByType()
        {
            this.FindMessageTypeNameByType<TestResponse>(
                "testResponse");
        }

        [Fact]
        public void MessageTypeResolverFindsEventTypeByName()
        {
            this.FindMessageTypeByName<TestEvent>(
                MessageType.Event,
                "testEvent");
        }

        [Fact]
        public void MessageTypeResolverFindsEventTypeNameByType()
        {
            this.FindMessageTypeNameByType<TestEvent>(
                "testEvent");
        }

        private void FindMessageTypeByName<TMessage>(
            MessageType messageType, 
            string messageTypeName)
        {
            Type concreteMessageType = null;

            bool isFound =
                this.messageTypeResolver.TryGetMessageTypeByName(
                    messageType,
                    messageTypeName,
                    out concreteMessageType);

            Assert.True(isFound);
            Assert.NotNull(concreteMessageType);
            Assert.Equal(typeof(TMessage), concreteMessageType);
        }

        private void FindMessageTypeNameByType<TMessage>(
            string expectedTypeName)
        {
            string returnedTypeName = null;

            bool isFound =
                this.messageTypeResolver.TryGetMessageTypeNameByType(
                    typeof(TMessage),
                    out returnedTypeName);

            Assert.True(isFound);
            Assert.NotNull(expectedTypeName);
            Assert.Equal(expectedTypeName, returnedTypeName);
        }
    }
}
