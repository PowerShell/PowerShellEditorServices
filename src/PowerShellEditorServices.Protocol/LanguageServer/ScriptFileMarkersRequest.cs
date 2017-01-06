//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    class ScriptFileMarkersRequest
    {
        public static readonly
            RequestType<ScriptFileMarkerRequestParams, ScriptFileMarkerRequestResultParams> Type =
                RequestType<ScriptFileMarkerRequestParams, ScriptFileMarkerRequestResultParams>.Create("powerShell/getScriptFileMarkers");
    }

    class ScriptFileMarkerRequestParams
    {
        public string filePath;
        public string[] rules;
        public string settings;
    }

    class ScriptFileMarkerRequestResultParams
    {
        public ScriptFileMarker[] markers;
    }
}
