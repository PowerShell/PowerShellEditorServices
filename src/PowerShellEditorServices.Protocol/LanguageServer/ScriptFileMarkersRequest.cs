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
    class ScriptFileMarkersRequest
    {
        public static readonly
            RequestType<ScriptFileMarkerRequestParams, ScriptFileMarkerRequestResultParams> Type =
                RequestType<ScriptFileMarkerRequestParams, ScriptFileMarkerRequestResultParams>.Create("powerShell/getScriptFileMarkers");
    }

    /// <summary>
    /// Class to encapsulate the request parameters.
    /// </summary>
    class ScriptFileMarkerRequestParams
    {
        /// <summary>
        /// Path of the file for which the markers are requested.
        /// </summary>
        public string filePath;

        /// <summary>
        /// Settings to provided to ScriptAnalyzer to get the markers.
        /// </summary>
        public string settings;
    }

    /// <summary>
    /// Class to encapsulate the result of marker request.
    /// </summary>
    class ScriptFileMarkerRequestResultParams
    {
        /// <summary>
        /// An array of markers obtained by analyzing the given file.
        /// </summary>
        public ScriptFileMarker[] markers;
    }
}
