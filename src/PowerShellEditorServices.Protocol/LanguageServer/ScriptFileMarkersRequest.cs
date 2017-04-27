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
    class ScriptFileMarkersRequest
    {
        public static readonly
            RequestType<ScriptFileMarkerRequestParams, ScriptFileMarkerRequestResultParams, object, object> Type =
                RequestType<ScriptFileMarkerRequestParams, ScriptFileMarkerRequestResultParams, object, object>.Create("powerShell/getScriptFileMarkers");
    }

    /// <summary>
    /// Class to encapsulate the request parameters.
    /// </summary>
    class ScriptFileMarkerRequestParams
    {
        /// <summary>
        /// Path of the file for which the markers are requested.
        /// </summary>
        public string fileUri;

        /// <summary>
        /// Settings to be provided to ScriptAnalyzer to get the markers.
        ///
        /// We have this unusual structure because JSON deserializer
        /// does not deserialize nested hashtables. i.e. it won't
        /// deserialize a hashtable within a hashtable. But in this case,
        /// i.e. a hashtable within a dictionary, it will deserialize
        /// the hashtable.
        /// </summary>
        public Dictionary<string, Hashtable> settings;
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
