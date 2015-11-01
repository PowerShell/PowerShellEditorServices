//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Request
{
    public abstract class RequestBase<TArgs> : MessageBase, IMessageProcessor
    {
        public string Command { get; set; }

        public TArgs Arguments { get; set; }

        internal override string PayloadType
        {
            get { return this.Command; }
            set { this.Command = value; }
        }

        public abstract Task ProcessMessage(
            EditorSession editorSession,
            MessageWriter messageWriter);

        public RequestBase()
        {
            this.Type = MessageType.Request;
        }

        protected ResponseBase<TResponseBody> PrepareResponse<TResponseBody>(
            ResponseBase<TResponseBody> response, 
            bool isSuccess = true)
        {
            response.RequestSeq = this.Seq;
            response.Success = true;

            return response;
        }
    }
}
