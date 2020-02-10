//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getVersion")]
    internal interface IGetVersionHandler : IJsonRpcRequestHandler<GetVersionParams, PowerShellVersion> { }

    internal class GetVersionParams : IRequest<PowerShellVersion> { }

    internal class PowerShellVersion
    {
        public string Version { get; set; }
        public string DisplayVersion { get; set; }
        public string Edition { get; set; }
        public string Architecture { get; set; }

        public PowerShellVersion()
        {
        }

        public PowerShellVersion(PowerShellVersionDetails versionDetails)
        {
            this.Version = versionDetails.VersionString;
            this.DisplayVersion = $"{versionDetails.Version.Major}.{versionDetails.Version.Minor}";
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
