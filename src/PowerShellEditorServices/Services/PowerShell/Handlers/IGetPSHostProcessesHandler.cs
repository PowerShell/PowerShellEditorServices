// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getPSHostProcesses")]
    internal interface IGetPSHostProcessesHandler : IJsonRpcRequestHandler<GetPSHostProcessesParams, PSHostProcessResponse[]> { }

    internal class GetPSHostProcessesParams : IRequest<PSHostProcessResponse[]> { }

    internal class PSHostProcessResponse
    {
        public string ProcessName { get; set; }

        public int ProcessId { get; set; }

        public string AppDomainName { get; set; }

        public string MainWindowTitle { get; set; }
    }
}
