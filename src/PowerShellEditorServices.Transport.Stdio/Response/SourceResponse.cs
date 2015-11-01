//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("source")]
    public class SourceResponse : ResponseBase<SourceResponseBody>
    {
    }

    public class SourceResponseBody
    {
        public string Content { get; set; }
    }
}

