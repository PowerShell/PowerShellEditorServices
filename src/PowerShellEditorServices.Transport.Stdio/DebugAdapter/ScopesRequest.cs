//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class ScopesRequest
    {
        public static readonly
            RequestType<ScopesRequestArguments, ScopesResponseBody, object> Type =
            RequestType<ScopesRequestArguments, ScopesResponseBody, object>.Create("scopes");
    }

    public class ScopesRequestArguments
    {
        public int FrameId { get; set; }
    }

    public class ScopesResponseBody
    {
        public Scope[] Scopes { get; set; }
    }
}

