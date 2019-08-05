using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace PowerShellEditorServices.Engine.Services.Handlers
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
