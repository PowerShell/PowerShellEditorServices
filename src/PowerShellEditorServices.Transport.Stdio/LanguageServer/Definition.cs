using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class DefinitionRequest
    {
        public static readonly
            RequestType<TextDocumentPosition, Location[], object> Type =
            RequestType<TextDocumentPosition, Location[], object>.Create("textDocument/definition");
    }
}
