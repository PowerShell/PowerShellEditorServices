//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    /// <summary>
    /// Class to encapsulate the request type.
    /// </summary>
    class ScriptRegionRequest
    {
        public static readonly
            RequestType<ScriptRegionRequestParams, ScriptRegionRequestResult> Type =
                RequestType<ScriptRegionRequestParams, ScriptRegionRequestResult>.Create("powerShell/getScriptRegion");
    }

    /// <summary>
    /// Class to encapsulate the request parameters.
    /// </summary>
    class ScriptRegionRequestParams
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
        /// 1-based line number of the character
        /// </summary>
        public int line;

        /// <summary>
        /// 1-based column number of the character
        /// </summary>
        public int column;
    }

    /// <summary>
    /// Class to encapsulate the result of marker request.
    /// </summary>
    class ScriptRegionRequestResult
    {
        /// <summary>
        /// A region in the script that encapsulates the given character/position which is suitable for
        /// for formatting
        /// </summary>
        public ScriptRegion scriptRegion;
    }
}
