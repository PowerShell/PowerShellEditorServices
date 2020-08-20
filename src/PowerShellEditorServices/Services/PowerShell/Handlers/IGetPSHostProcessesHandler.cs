//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getPSHostProcesses")]
    internal interface IGetPSHostProcessesHandler : IJsonRpcRequestHandler<GetPSHostProcesssesParams, PSHostProcessResponse[]> { }

    internal class GetPSHostProcesssesParams : IRequest<PSHostProcessResponse[]> { }

    internal class PSHostProcessResponse
    {
        public string ProcessName { get; set; }

        public int ProcessId { get; set; }

        public string AppDomainName { get; set; }

        public string MainWindowTitle { get; set; }
    }
}
