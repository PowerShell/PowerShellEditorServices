//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    // NOTE: Not clear what message type name this has based on protocol...
    [MessageTypeName("error")]
    public class ErrorResponse : ResponseBase<ErrorResponseBody>
    {
    }

    public class ErrorResponseBody
    {
        public string Error { get; set; }
    }
}

