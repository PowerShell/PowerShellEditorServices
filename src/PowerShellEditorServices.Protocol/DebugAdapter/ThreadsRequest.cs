//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class ThreadsRequest
    {
        public static readonly
            RequestType<object, ThreadsResponseBody> Type =
            RequestType<object, ThreadsResponseBody>.Create("threads");
    }

    public class ThreadsResponseBody
    {
        public Thread[] Threads { get; set; }
    }
}

