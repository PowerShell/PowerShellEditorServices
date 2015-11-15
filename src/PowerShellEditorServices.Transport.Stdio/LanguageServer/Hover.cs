using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Protocol.LanguageServer
{
    public class MarkedString
    {
        public string Language { get; set; }

        public string Value { get; set; }
    }

    public class Hover
    {
        public MarkedString[] Contents { get; set; }

        public Range? Range { get; set; }
    }

    public class HoverRequest
    {
        public static readonly
            RequestType<TextDocumentPosition, Hover, object> Type =
            RequestType<TextDocumentPosition, Hover, object>.Create("textDocument/hover");

    }
}
