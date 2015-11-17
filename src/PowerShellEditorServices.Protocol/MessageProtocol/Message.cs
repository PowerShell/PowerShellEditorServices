//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public enum MessageType
    {
        Unknown,
        Request,
        Response,
        Event
    }

    public class Message
    {
        public MessageType MessageType { get; set; }

        public string Id { get; set; }

        public string Method { get; set; }

        public JToken Contents { get; set; }

        public JToken Error { get; set; }

        public static Message Unknown()
        {
            return new Message
            {
                MessageType = MessageType.Unknown
            };
        }

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

