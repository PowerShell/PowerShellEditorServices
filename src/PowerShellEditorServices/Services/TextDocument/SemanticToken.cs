using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    internal class SemanticToken
    {
        public SemanticToken(string text, SemanticTokenType type, int line, int index, IEnumerable<string> tokenModifiers)
        {
            Line = line;
            Text = text;
            Index = index;
            Type = type;
            TokenModifiers = tokenModifiers;
        }

        public string Text {get; set;}

        public int Line {get; set;}

        public int Index {get; set;}

        public SemanticTokenType Type {get; set;}

        public IEnumerable<string> TokenModifiers {get; set;}
    }
}
