//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System.Collections.Generic;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    /// <summary>
    /// Class to encapsulate the request type.
    /// </summary>
    class FormattingScriptRegionRequest
    {
        public static readonly
            RequestType<FormattingScriptRegionRequestParams, FormattingScriptRegionRequestResult> Type =
                RequestType<FormattingScriptRegionRequestParams, FormattingScriptRegionRequestResult>.Create("powerShell/getFormattingScriptRegion");
    }

    /// <summary>
    /// Class to encapsulate the request parameters.
    /// </summary>
    class FormattingScriptRegionRequestParams
    {
        /// <summary>
        /// Path of the file for which the markers are requested.
        /// </summary>
        public string fileUri;

        /// <summary>
        /// Hint character
        /// </summary>
        public string character;

        /// <summary>
        /// Character position
        /// </summary>
        public FilePosition filePosition;
    }

    /// <summary>
    /// Class to encapsulate the result of marker request.
    /// </summary>
    class FormattingScriptRegionRequestResult
    {
        /// <summary>
        /// A region in the script that encapsulates the given character/position which is suitable for
        /// for formatting
        /// </summary>
        public ScriptRegion scriptRegion;
    }
}
