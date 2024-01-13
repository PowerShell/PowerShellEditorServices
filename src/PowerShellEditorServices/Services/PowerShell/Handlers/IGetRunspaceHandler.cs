// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getRunspace")]
    internal interface IGetRunspaceHandler : IJsonRpcRequestHandler<GetRunspaceParams, RunspaceResponse[]> { }

    internal class GetRunspaceParams : IRequest<RunspaceResponse[]>
    {
        public int ProcessId { get; set; }
    }

    internal class RunspaceResponse
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Availability { get; set; }
    }
}
