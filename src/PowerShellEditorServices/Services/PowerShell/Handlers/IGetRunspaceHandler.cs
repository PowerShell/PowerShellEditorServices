//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getRunspace")]
    internal interface IGetRunspaceHandler : IJsonRpcRequestHandler<GetRunspaceParams, RunspaceResponse[]> { }

    internal class GetRunspaceParams : IRequest<RunspaceResponse[]>
    {
        public string ProcessId {get; set; }
    }

    internal class RunspaceResponse
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Availability { get; set; }
    }
}
