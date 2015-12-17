//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    /// <summary>
    /// Defines all possible message types.
    /// </summary>
    public enum MessageType
    {
        Unknown,
        Request,
        Response,
        Event
    }

    /// <summary>
    /// Provides common details for protocol messages of any format.
    /// </summary>
    [DebuggerDisplay("MessageType = {MessageType.ToString()}, Method = {Method}, Id = {Id}")]
    public class Message
    {
        /// <summary>
        /// Gets or sets the message type.
        /// </summary>
        public MessageType MessageType { get; set; }

        /// <summary>
        /// Gets or sets the message's sequence ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the message's method/command name.
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets a JToken containing the contents of the message.
        /// </summary>
        public JToken Contents { get; set; }

        /// <summary>
        /// Gets or sets a JToken containing error details.
        /// </summary>
        public JToken Error { get; set; }
        
        /// <summary>
        /// Creates a message with an Unknown type.
        /// </summary>
        /// <returns>A message with Unknown type.</returns>
        public static Message Unknown()
        {
            return new Message
            {
                MessageType = MessageType.Unknown
            };
        }

        /// <summary>
        /// Creates a message with a Request type.
        /// </summary>
        /// <param name="id">The sequence ID of the request.</param>
        /// <param name="method">The method name of the request.</param>
        /// <param name="contents">The contents of the request.</param>
        /// <returns>A message with a Request type.</returns>
        public static Message Request(string id, string method, JToken contents)
        {
            return new Message
            {
                MessageType = MessageType.Request,
                Id = id,
                Method = method,
                Contents = contents
            };
        }

        /// <summary>
        /// Creates a message with a Response type.
        /// </summary>
        /// <param name="id">The sequence ID of the original request.</param>
        /// <param name="method">The method name of the original request.</param>
        /// <param name="contents">The contents of the response.</param>
        /// <returns>A message with a Response type.</returns>
        public static Message Response(string id, string method, JToken contents)
        {
            return new Message
            {
                MessageType = MessageType.Response,
                Id = id,
                Method = method,
                Contents = contents
            };
        }

        /// <summary>
        /// Creates a message with a Response type and error details.
        /// </summary>
        /// <param name="id">The sequence ID of the original request.</param>
        /// <param name="method">The method name of the original request.</param>
        /// <param name="error">The error details of the response.</param>
        /// <returns>A message with a Response type and error details.</returns>
        public static Message ResponseError(string id, string method, JToken error)
        {
            return new Message
            {
                MessageType = MessageType.Response,
                Id = id,
                Method = method,
                Error = error
            };
        }

        /// <summary>
        /// Creates a message with an Event type.
        /// </summary>
        /// <param name="method">The method name of the event.</param>
        /// <param name="contents">The contents of the event.</param>
        /// <returns>A message with an Event type.</returns>
        public static Message Event(string method, JToken contents)
        {
            return new Message
            {
                MessageType = MessageType.Event,
                Method = method,
                Contents = contents
            };
        }
    }
}

