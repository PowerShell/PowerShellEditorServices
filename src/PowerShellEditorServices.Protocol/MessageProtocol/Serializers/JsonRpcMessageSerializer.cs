//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Serializers
{
    /// <summary>
    /// Serializes messages in the JSON RPC format.  Used primarily
    /// for language servers.
    /// </summary>
    public class JsonRpcMessageSerializer : IMessageSerializer
    {
        public JObject SerializeMessage(Message message)
        {
            JObject messageObject = new JObject();

            messageObject.Add("jsonrpc", JToken.FromObject("2.0"));

            if (message.MessageType == MessageType.Request)
            {
                messageObject.Add("id", JToken.FromObject(message.Id));
                messageObject.Add("method", message.Method);
                messageObject.Add("params", message.Contents);
            }
            else if (message.MessageType == MessageType.Event)
            {
                messageObject.Add("method", message.Method);
                messageObject.Add("params", message.Contents);
            }
            else if (message.MessageType == MessageType.Response)
            {
                messageObject.Add("id", JToken.FromObject(message.Id));

                if (message.Error != null)
                {
                    // Write error
                    messageObject.Add("error", message.Error);
                }
                else
                {
                    // Write result
                    messageObject.Add("result", message.Contents);
                }
            }

            return messageObject;
        }

        public Message DeserializeMessage(JObject messageJson)
        {
            // TODO: Check for jsonrpc version

            JToken token = null;
            if (messageJson.TryGetValue("id", out token))
            {
                // Message is a Request or Response
                string messageId = token.ToString();

                if (messageJson.TryGetValue("result", out token))
                {
                    return Message.Response(messageId, null, token);
                }
                else if (messageJson.TryGetValue("error", out token))
                {
                    return Message.ResponseError(messageId, null, token);
                }
                else
                {
                    JToken messageParams = null;
                    messageJson.TryGetValue("params", out messageParams);

                    if (!messageJson.TryGetValue("method", out token))
                    {
                        // TODO: Throw parse error
                    }

                    return Message.Request(messageId, token.ToString(), messageParams);
                }
            }
            else
            {
                // Messages without an id are events
                JToken messageParams = token;
                messageJson.TryGetValue("params", out messageParams);

                if (!messageJson.TryGetValue("method", out token))
                {
                    // TODO: Throw parse error
                }

                return Message.Event(token.ToString(), messageParams);
            }
        }
    }
}

