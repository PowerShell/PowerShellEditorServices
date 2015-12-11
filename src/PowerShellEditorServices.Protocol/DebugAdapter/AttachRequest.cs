//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class AttachRequest
    {
        public static readonly
            RequestType<AttachRequestArguments, object> Type =
            RequestType<AttachRequestArguments, object>.Create("attach");
    }

    public class AttachRequestArguments
    {
        public string Address { get; set; }

        public int Port { get; set; }
    }
}
