//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    public class MessageErrorResponse : ResponseBase<MessageErrorResponseDetails>
    {
        private MessageErrorResponse()
        {
            // This class always returns an error
            this.Success = false;
        }

        public static MessageErrorResponse CreateUnhandledMessageResponse(
            MessageBase unhandledMessage)
        {
            return new MessageErrorResponse
            {
                RequestSeq = unhandledMessage.Seq,
                Body = new MessageErrorResponseDetails
                {
                    ErrorMessage = "A message was not able to be handled by the service.",
                    MessageType = unhandledMessage.Type,
                    PayloadType = unhandledMessage.PayloadType
                }
            };
        }

        public static MessageErrorResponse CreateParseErrorResponse(
            MessageParseException parseException)
        {
            return new MessageErrorResponse
            {
                Body = new MessageErrorResponseDetails
                {
                    ErrorMessage = 
                        string.Format(
                            "A message was not able to be parsed by the service: {0}",
                            parseException.OriginalMessageText),
                    MessageType = "unknown",
                    PayloadType = "unknown"
                }
            };
        }
    }

    public class MessageErrorResponseDetails
    {
        public string ErrorMessage { get; set; }

        public string MessageType { get; set; }

        public string PayloadType { get; set; }
    }
}
