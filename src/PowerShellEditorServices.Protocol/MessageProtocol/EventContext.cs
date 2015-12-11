//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    /// <summary>
    /// Provides context for a received event so that handlers
    /// can write events back to the channel.
    /// </summary>
    public class EventContext
    {
        private MessageWriter messageWriter;

        public EventContext(MessageWriter messageWriter)
        {
            this.messageWriter = messageWriter;
        }

        public async Task SendEvent<TParams>(
            EventType<TParams> eventType, 
            TParams eventParams)
        {
            await this.messageWriter.WriteEvent(
                eventType,
                eventParams);
        }
    }
}

