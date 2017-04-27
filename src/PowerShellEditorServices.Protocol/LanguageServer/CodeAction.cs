using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class CodeActionRequest
    {
        public static readonly
            RequestType<CodeActionParams, CodeActionCommand[], object, object> Type =
            RequestType<CodeActionParams, CodeActionCommand[], object, object>.Create("textDocument/codeAction");
    }

    /// <summary>
    /// Parameters for CodeActionRequest.
    /// </summary>
    public class CodeActionParams
    {
        /// <summary>
        /// The document in which the command was invoked.
        /// </summary>
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <summary>
        /// The range for which the command was invoked.
        /// </summary>
        public Range Range { get; set; }

        /// <summary>
        /// Context carrying additional information.
        /// </summary>
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

        public JArray Arguments { get; set; }
    }
}
