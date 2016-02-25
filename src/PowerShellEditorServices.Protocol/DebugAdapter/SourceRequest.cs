//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class SourceRequest
    {
        public static readonly
            RequestType<SourceRequestArguments, SourceResponseBody> Type =
            RequestType<SourceRequestArguments, SourceResponseBody>.Create("source");
    }

    public class SourceRequestArguments
    {
        /// <summary>
        /// Gets or sets the reference to the source. This is the value received in Source.reference.
        /// </summary>
        public int SourceReference { get; set; }
    }

    public class SourceResponseBody
    {
        public string Content { get; set; }
    }
}
