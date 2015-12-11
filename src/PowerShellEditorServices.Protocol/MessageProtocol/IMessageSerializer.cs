//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    /// <summary>
    /// Defines a common interface for message serializers.
    /// </summary>
    public interface IMessageSerializer
    {
        /// <summary>
        /// Serializes a Message to a JObject.
        /// </summary>
        /// <param name="message">The message to be serialized.</param>
        /// <returns>A JObject which contains the JSON representation of the message.</returns>
        JObject SerializeMessage(Message message);

        /// <summary>
        /// Deserializes a JObject to a Messsage.
        /// </summary>
        /// <param name="messageJson">The JObject containing the JSON representation of the message.</param>
        /// <returns>The Message that was represented by the JObject.</returns>
        Message DeserializeMessage(JObject messageJson);
    }
}

