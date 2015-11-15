using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class SignatureHelpRequest
    {
        public static readonly
            RequestType<TextDocumentPosition, SignatureHelp, object> Type =
            RequestType<TextDocumentPosition, SignatureHelp, object>.Create("textDocument/signatureHelp");
    }

    public class ParameterInformation
    {
        public string Label { get; set; }

        public string Documentation { get; set; }
    }

    public class SignatureInformation
    {
        public string Label { get; set; }

        public string Documentation { get; set; }

        public ParameterInformation[] Parameters { get; set; }
    }

    public class SignatureHelp
    {
        public SignatureInformation[] Signatures { get; set; }

        public int? ActiveSignature { get; set; }

        public int? ActiveParameter { get; set; }
    }
}
