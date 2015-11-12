//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Newtonsoft.Json;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    public abstract class ResponseBase<TBody> : MessageBase
    {
        [JsonProperty("request_seq")]
        public int RequestSeq { get; set; }

        public bool Success { get; set; }

        public string Command { get; set; }

        public string Message { get; set; }

        public TBody Body { get; set; }

        internal override string PayloadType
        {
            get { return this.Command; }
            set { this.Command = value; }
        }

        public ResponseBase()
        {
            this.Type = MessageType.Response;
        }
    }
}
