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
            RequestType<InitializeRequestArguments, InitializeResponseBody> Type =
            RequestType<InitializeRequestArguments, InitializeResponseBody>.Create("initialize");
    }

    public class InitializeRequestArguments
    {
        public string AdapterId { get; set; }

        public bool LinesStartAt1 { get; set; }

        public string PathFormat { get; set; }

        public bool SourceMaps { get; set; }

        public string GeneratedCodeDirectory { get; set; }
    }

    public class InitializeResponseBody
    {
        /// <summary>
        /// Gets or sets a boolean value that determines whether the debug adapter 
        /// supports the configurationDoneRequest.
        /// </summary>
        public bool SupportsConfigurationDoneRequest { get; set; }

        /// <summary>
        /// Gets or sets a boolean value that determines whether the debug adapter 
        /// supports functionBreakpoints.
        /// </summary>
        public bool SupportsFunctionBreakpoints { get; set; }

        /// <summary>
        /// Gets or sets a boolean value that determines whether the debug adapter 
        /// supports conditionalBreakpoints.
        /// </summary>
        public bool SupportsConditionalBreakpoints { get; set; }

        /// <summary>
        /// Gets or sets a boolean value that determines whether the debug adapter 
        /// supports a (side effect free) evaluate request for data hovers.
        /// </summary>
        public bool SupportsEvaluateForHovers { get; set; }
    }
}
