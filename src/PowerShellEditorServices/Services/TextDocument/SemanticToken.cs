using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    internal class SemanticToken
    {
        public SemanticToken(string text, SemanticTokenType type, int line, int column, IEnumerable<string> tokenModifiers)
        {
            Line = line;
            Text = text;
            Column = column;
            Type = type;
            TokenModifiers = tokenModifiers;
        }

        public string Text { get; set ;}

        public int Line { get; set; }

        public int Column { get; set; }

        public SemanticTokenType Type { get; set; }

        public IEnumerable<string> TokenModifiers { get; set; }
    }
}
