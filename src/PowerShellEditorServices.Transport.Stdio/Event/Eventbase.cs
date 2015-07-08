//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Newtonsoft.Json;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Event
{
    public abstract class EventBase<TBody> : MessageBase, IEvent
    {
        [JsonProperty("event")]
        public string EventType { get; set; }

        public TBody Body { get; set; }

        internal override string PayloadType
        {
            get { return this.EventType; }
        }

        public EventBase()
        {
            this.Type = "event";
        }
    }
}
