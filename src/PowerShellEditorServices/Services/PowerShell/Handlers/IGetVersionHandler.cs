// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MediatR;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Context;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
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
            Version = versionDetails.VersionString;
            DisplayVersion = $"{versionDetails.Version.Major}.{versionDetails.Version.Minor}";
            Edition = versionDetails.Edition;

            Architecture = versionDetails.Architecture switch
            {
                PowerShellProcessArchitecture.X64 => "x64",
                PowerShellProcessArchitecture.X86 => "x86",
                _ => "Architecture Unknown",
            };
        }
    }
}
