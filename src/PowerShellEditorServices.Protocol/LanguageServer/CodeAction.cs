using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class CodeActionRequest
    {
        public static readonly
            RequestType<CodeActionRequest, CodeActionCommand[]> Type =
            RequestType<CodeActionRequest, CodeActionCommand[]>.Create("textDocument/codeAction");

        public TextDocumentIdentifier TextDocument { get; set; }

        public Range Range { get; set; }

        public CodeActionContext Context { get; set; }
    }

    public class CodeActionContext
    {
        public Diagnostic[] Diagnostics { get; set; }
    }

    public class CodeActionCommand
    {
        public string Title { get; set; }

        public string Command { get; set; }

        public object Arguments { get; set; }
    }
}
