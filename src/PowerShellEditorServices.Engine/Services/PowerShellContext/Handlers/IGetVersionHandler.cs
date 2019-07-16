using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    [Serial, Method("powerShell/getVersion")]
    public interface IGetVersionHandler : IJsonRpcRequestHandler<GetVersionParams, PowerShellVersionDetails> { }

    public class GetVersionParams : IRequest<PowerShellVersionDetails> { }

    public class PowerShellVersionDetails {
        public string Version { get; set; }
        public string DisplayVersion { get; set; }
        public string Edition { get; set; }
        public string Architecture { get; set; }
    }
}
