//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    [Serial, Method("powerShell/getPSHostProcesses")]
    public interface IGetPSHostProcessesHandler : IJsonRpcRequestHandler<GetPSHostProcesssesParams, PSHostProcessResponse[]> { }

    public class GetPSHostProcesssesParams : IRequest<PSHostProcessResponse[]> { }

    public class PSHostProcessResponse
    {
        public string ProcessName { get; set; }

        public int ProcessId { get; set; }

        public string AppDomainName { get; set; }

        public string MainWindowTitle { get; set; }
    }
}
