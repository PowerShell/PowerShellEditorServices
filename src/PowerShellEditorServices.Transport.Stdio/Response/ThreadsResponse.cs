//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Model;

namespace Microsoft.PowerShell.EditorServices.Transport.Stdio.Response
{
    [MessageTypeName("threads")]
    public class ThreadsResponse : ResponseBase<ThreadsResponseBody>
    {
    }

    public class ThreadsResponseBody
    {
        public Thread[] Threads { get; set; }
    }
}

