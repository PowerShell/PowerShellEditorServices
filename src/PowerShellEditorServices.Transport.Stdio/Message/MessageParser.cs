//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Message
{
    public class MessageParser
    {
        #region Private Fields

        private JsonSerializer jsonSerializer = 
            JsonSerializer.Create(
                Constants.JsonSerializerSettings);

        private MessageTypeResolver messageTypeResolver;

        #endregion

        #region Constructors

        public MessageParser(MessageTypeResolver messageTypeResolver)
        {
            Validate.IsNotNull("messageTypeResolver", messageTypeResolver);

            this.messageTypeResolver = messageTypeResolver;
        }

        #endregion

        #region Public Methods

        public MessageBase ParseMessage(string messageJson)
        {
            string messageTypeName = null;
            Type concreteMessageType = null;
            MessageType messageType = MessageType.Unknown;

            // Parse the JSON string to a JObject
            JObject messageObject = JObject.Parse(messageJson);

            // Get the message type and name from the JSON object
            if (!this.TryGetMessageTypeAndName(
                messageObject,
                out messageType,
                out messageTypeName))
            {
                throw new MessageParseException(
                    messageObject.ToString(),
                    "Unknown message type: {0}",
                    messageTypeName);
            }

            // Look up the message type by name
            if (!this.messageTypeResolver.TryGetMessageTypeByName(
                messageType,
                messageTypeName,
                out concreteMessageType))
            {
                throw new MessageParseException(
                    messageObject.ToString(),
                    "Could not locate message type by name: {0}",
                    messageTypeName);
            }

            // Return the deserialized message
            return
                (MessageBase)messageObject.ToObject(
                    concreteMessageType,
                    this.jsonSerializer);
        }

        #endregion

        #region Private Helper Methods

        private bool TryGetMessageTypeAndName(
            JObject messageObject,
            out MessageType messageType,
            out string messageTypeName)
        {
            messageType = MessageType.Unknown;
            messageTypeName = null;

            if (TryGetValueString(messageObject, "type", out messageTypeName))
            {
                switch (messageTypeName)
                {
                    case "request":
                        messageType = MessageType.Request;
                        return TryGetValueString(messageObject, "command", out messageTypeName);

                    case "response":
                        messageType = MessageType.Response;
                        return TryGetValueString(messageObject, "command", out messageTypeName);

                    case "event":
                        messageType = MessageType.Event;
                        return TryGetValueString(messageObject, "event", out messageTypeName);

                    default:
                        return false;
                }
            }

            return false;
        }

        private static bool TryGetValueString(JObject jsonObject, string valueName, out string valueString)
        {
            valueString = null;

            JToken valueToken = null;
            if (jsonObject.TryGetValue(valueName, out valueToken))
            {
                JValue realValueToken = valueToken as JValue;
                if (realValueToken != null)
                {
                    if (realValueToken.Type == JTokenType.String)
                    {
                        valueString = (string)realValueToken.Value;
                    }
                    else if (realValueToken.Type == JTokenType.Null)
                    {
                        // If the value is null, return it too
                        valueString = null;
                    }
                    else
                    {
                        // No other value type is valid
                        return false;
                    }

                    return true;
                }
                else
                {
                    // TODO: Trace unexpected condition
                }
            }

            return false;
        }

        #endregion
    }
}
