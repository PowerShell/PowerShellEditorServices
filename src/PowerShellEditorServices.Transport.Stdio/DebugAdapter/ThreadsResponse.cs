//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
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

