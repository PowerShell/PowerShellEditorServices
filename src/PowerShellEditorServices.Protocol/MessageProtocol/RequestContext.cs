//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class RequestContext<TResult>
    {
        private Message requestMessage;
        private MessageWriter messageWriter;

        public RequestContext(Message requestMessage, MessageWriter messageWriter)
        {
            this.requestMessage = requestMessage;
            this.messageWriter = messageWriter;
        }

        public async Task SendResult(TResult resultDetails)
        {
            await this.messageWriter.WriteResponse<TResult>(
                resultDetails,
                requestMessage.Method,
                requestMessage.Id);
        }

        public async Task SendEvent<TParams>(EventType<TParams> eventType, TParams eventParams)
        {
            await this.messageWriter.WriteEvent(
                eventType,
                eventParams);
        }

        public async Task SendError(object errorDetails)
        {
            await this.messageWriter.WriteMessage(
                Message.ResponseError(
                    requestMessage.Id,
                    requestMessage.Method,
                    JToken.FromObject(errorDetails)));
        }
    }
}

