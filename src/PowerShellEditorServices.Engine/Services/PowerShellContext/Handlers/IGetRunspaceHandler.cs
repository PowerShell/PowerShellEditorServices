//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Engine.Handlers
{
    [Serial, Method("powerShell/getRunspace")]
    public interface IGetRunspaceHandler : IJsonRpcRequestHandler<GetRunspaceParams, RunspaceResponse[]> { }

    public class GetRunspaceParams : IRequest<RunspaceResponse[]>
    {
        public string ProcessId {get; set; }
    }

    public class RunspaceResponse
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Availability { get; set; }
    }
}
