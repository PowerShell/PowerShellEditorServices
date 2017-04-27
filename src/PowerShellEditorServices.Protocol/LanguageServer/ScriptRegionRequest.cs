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
            RequestType<ScriptRegionRequestParams, ScriptRegionRequestResult, object, object> Type =
                RequestType<ScriptRegionRequestParams, ScriptRegionRequestResult, object, object>.Create("powerShell/getScriptRegion");
    }

    /// <summary>
    /// Class to encapsulate the request parameters.
    /// </summary>
    class ScriptRegionRequestParams
    {
        /// <summary>
        /// Path of the file for which the formatting region is requested.
        /// </summary>
        public string FileUri;

        /// <summary>
        /// Hint character.
        /// </summary>
        public string Character;

        /// <summary>
        /// 1-based line number of the character.
        /// </summary>
        public int Line;

        /// <summary>
        /// 1-based column number of the character.
        /// </summary>
        public int Column;
    }

    /// <summary>
    /// Class to encapsulate the result of ScriptRegionRequest.
    /// </summary>
    class ScriptRegionRequestResult
    {
        /// <summary>
        /// A region in the script that encapsulates the given character/position which is suitable
        /// for formatting
        /// </summary>
        public ScriptRegion scriptRegion;
    }
}
