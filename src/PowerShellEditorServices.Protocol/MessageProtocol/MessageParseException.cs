//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;

namespace Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol
{
    public class MessageParseException : Exception
    {
        public string OriginalMessageText { get; private set; }

        public MessageParseException(
            string originalMessageText, 
            string errorMessage, 
            params object[] errorMessageArgs) 
            : base(string.Format(errorMessage, errorMessageArgs))
        {
            this.OriginalMessageText = originalMessageText;
        }
    }
}
