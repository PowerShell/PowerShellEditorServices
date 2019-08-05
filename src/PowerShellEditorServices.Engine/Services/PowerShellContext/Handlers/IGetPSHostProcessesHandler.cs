using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace PowerShellEditorServices.Engine.Services.Handlers
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
