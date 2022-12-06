// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell
{
    [Serial, Method("powerShell/getVersion")]
    internal interface IGetVersionHandler : IJsonRpcRequestHandler<GetVersionParams, PowerShellVersion> { }

    internal class GetVersionParams : IRequest<PowerShellVersion> { }

    internal record PowerShellVersion
    {
        public string Version { get; init; }
        public string Edition { get; init; }
        public string Commit { get; init; }
        public string Architecture { get; init; }
    }
}
