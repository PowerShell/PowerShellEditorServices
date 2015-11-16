//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter
{
    public class InitializeRequest
    {
        public static readonly
            RequestType<InitializeRequestArguments, object, object> Type =
            RequestType<InitializeRequestArguments, object, object>.Create("initialize");
    }

    public class InitializeRequestArguments
    {
        public string AdapterId { get; set; }

        public bool LinesStartAt1 { get; set; }

        public string PathFormat { get; set; }

        public bool SourceMaps { get; set; }

        public string GeneratedCodeDirectory { get; set; }
    }
}
