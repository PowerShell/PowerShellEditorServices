//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Session;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class PowerShellVersionRequest
    {
        public static readonly
            RequestType<object, PowerShellVersionResponse> Type =
            RequestType<object, PowerShellVersionResponse>.Create("powerShell/getVersion");
    }

    public class PowerShellVersionResponse
    {
        public string Version { get; set; }

        public string DisplayVersion { get; set; }

        public string Edition { get; set; }

        public string Architecture { get; set; }

        public PowerShellVersionResponse()
        {
        }

        public PowerShellVersionResponse(PowerShellVersionDetails versionDetails)
        {
            this.Version = versionDetails.Version.ToString();
            this.DisplayVersion = versionDetails.VersionString;
            this.Edition = versionDetails.Edition;
            this.Architecture = versionDetails.Architecture;
        }
    }
}
