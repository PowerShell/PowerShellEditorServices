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
            this.Version = versionDetails.VersionString;
            this.DisplayVersion =  $"{versionDetails.Version.Major}.{versionDetails.Version.Minor}";
            this.Edition = versionDetails.Edition;

            switch (versionDetails.Architecture)
            {
                case PowerShellProcessArchitecture.X64:
                    this.Architecture = "x64";
                    break;
                case PowerShellProcessArchitecture.X86:
                    this.Architecture = "x86";
                    break;
                default:
                    this.Architecture = "Architecture Unknown";
                    break;
            }
        }
    }
}
