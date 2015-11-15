using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class ReferencesRequest
    {
        public static readonly
            RequestType<ReferencesParams, Location[], object> Type =
            RequestType<ReferencesParams, Location[], object>.Create("textDocument/references");
    }

    public class ReferencesParams : TextDocumentPosition
    {
        public ReferencesOptions Options { get; set; }
    }

    public class ReferencesOptions
    {
        public bool IncludeDeclaration { get; set; }
    }
}
