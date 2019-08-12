using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    [Serial, Method("powerShell/invokeExtensionCommand")]
    public interface IInvokeExtensionCommandHandler : IJsonRpcNotificationHandler<InvokeExtensionCommandParams> { }

    public class InvokeExtensionCommandParams : IRequest
    {
        public string Name { get; set; }

        public ClientEditorContext Context { get; set; }
    }

    public class ClientEditorContext
    {
        public string CurrentFileContent { get; set; }

        public string CurrentFileLanguage { get; set; }

        public string CurrentFilePath { get; set; }

        public Position CursorPosition { get; set; }

        public Range SelectionRange { get; set; }

    }
}
