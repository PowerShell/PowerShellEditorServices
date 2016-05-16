//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Serializers;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel
{
    /// <summary>
    /// Defines a base implementation for servers and their clients over a
    /// single kind of communication channel.
    /// </summary>
    public abstract class ChannelBase
    {
        /// <summary>
        /// Gets a boolean that is true if the channel is connected or false if not.
        /// </summary>
        public bool IsConnected { get; protected set; }

        /// <summary>
        /// Gets the MessageReader for reading messages from the channel.
        /// </summary>
        public MessageReader MessageReader { get; protected set; }

        /// <summary>
        /// Gets the MessageWriter for writing messages to the channel.
        /// </summary>
        public MessageWriter MessageWriter { get; protected set; }

        /// <summary>
        /// Starts the channel and initializes the MessageDispatcher.
        /// </summary>
        /// <param name="messageProtocolType">The type of message protocol used by the channel.</param>
        public void Start(MessageProtocolType messageProtocolType)
        {
            IMessageSerializer messageSerializer = null;
            if (messageProtocolType == MessageProtocolType.LanguageServer)
            {
                messageSerializer = new JsonRpcMessageSerializer();
            }
            else
            {
                messageSerializer = new V8MessageSerializer();
            }

            this.Initialize(messageSerializer);
        }

        /// <summary>
        /// Returns a Task that allows the consumer of the ChannelBase
        /// implementation to wait until a connection has been made to
        /// the opposite endpoint whether it's a client or server.
        /// </summary>
        /// <returns>A Task to be awaited until a connection is made.</returns>
        public abstract Task WaitForConnection();

        /// <summary>
        /// Stops the channel.
        /// </summary>
        public void Stop()
        {
            this.Shutdown();
        }

        /// <summary>
        /// A method to be implemented by subclasses to handle the
        /// actual initialization of the channel and the creation and
        /// assignment of the MessageReader and MessageWriter properties.
        /// </summary>
        /// <param name="messageSerializer">The IMessageSerializer to use for message serialization.</param>
        protected abstract void Initialize(IMessageSerializer messageSerializer);

        /// <summary>
        /// A method to be implemented by subclasses to handle shutdown
        /// of the channel once Stop is called.
        /// </summary>
        protected abstract void Shutdown();
    }
}
