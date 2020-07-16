using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using System.Management.Automation.Language;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document.Proposals;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
#pragma warning disable 618
    internal class SemanticTokens : SemanticTokensHandler
    {
        private readonly ILogger _logger;
        private UTF8Encoding _utf8;
        public WorkspaceService _workspaceService;
        public SemanticTokens(ILogger<SemanticTokens> logger, WorkspaceService workspaceService) : base(new SemanticTokensRegistrationOptions() {
            DocumentSelector = DocumentSelector.ForLanguage("powershell"),
            Legend = new SemanticTokensLegend(),
            DocumentProvider = new Supports<SemanticTokensDocumentProviderOptions>(true,
                new SemanticTokensDocumentProviderOptions() {
                    Edits = true
                }),
            RangeProvider = true
        })
        {
            _logger = logger;
            _utf8 = new UTF8Encoding();
            _workspaceService = workspaceService;
        }

        public override async Task<OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals.SemanticTokens> Handle(
            SemanticTokensParams request, CancellationToken cancellationToken)
        {
            var result = await base.Handle(request, cancellationToken);
            return result;
        }

        public override async Task<SemanticTokensOrSemanticTokensEdits> Handle(SemanticTokensEditsParams request,
            CancellationToken cancellationToken)
        {
            var result = await base.Handle(request, cancellationToken);
            return result;
        }

        protected override async Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier,
            CancellationToken cancellationToken)
        {
            ScriptFile file = _workspaceService.GetFile(DocumentUri.GetFileSystemPath(identifier));
            await Task.Yield();
            Token[] tokens = file.ScriptTokens;
            foreach (var token in tokens){
                pushToken(token, builder);
            }
        }

        private static void pushToken(Token token, SemanticTokensBuilder builder){
            if(token is StringExpandableToken stringExpandableToken){
                if(stringExpandableToken.NestedTokens != null)
                {
                    foreach(Token t in stringExpandableToken.NestedTokens){
                        pushToken(t, builder);
                    }
                    return;
                }
            }

            var line = token.Extent.StartLineNumber - 1;
            var index = token.Extent.StartColumnNumber - 1;
            var length = token.Text.Length;
            var type = token.Kind;

            builder.Push(line, index, length, MapSemanticToken(token), new string[]{});
        }

        private static SemanticTokenType MapSemanticToken(Token token)
        {
            //first check token flags
            if ((token.TokenFlags & TokenFlags.Keyword) != 0)
            {
                return SemanticTokenType.Keyword;
            }

            if ((token.TokenFlags & TokenFlags.CommandName) != 0)
            {
                return SemanticTokenType.Function;
            }

            if (token.Kind != TokenKind.Generic && (token.TokenFlags &
                (TokenFlags.BinaryOperator | TokenFlags.UnaryOperator | TokenFlags.AssignmentOperator )) != 0)
            {
                return SemanticTokenType.Operator;
            }

            if ((token.TokenFlags & TokenFlags.TypeName) != 0)
            {
                return SemanticTokenType.Type;
            }

            if ((token.TokenFlags & TokenFlags.MemberName) != 0)
            {
                return SemanticTokenType.Member;
            }

            // Only check token kind after checking flags
            switch (token.Kind)
            {
                case TokenKind.Comment:
                    return SemanticTokenType.Comment;

                case TokenKind.Parameter:
                case TokenKind.Generic when token is StringLiteralToken slt && slt.Text.StartsWith("--"):
                    return SemanticTokenType.Parameter;

                case TokenKind.Variable:
                case TokenKind.SplattedVariable:
                    return SemanticTokenType.Variable;

                case TokenKind.StringExpandable:
                case TokenKind.StringLiteral:
                case TokenKind.HereStringExpandable:
                case TokenKind.HereStringLiteral:
                    return SemanticTokenType.String;

                case TokenKind.Number:
                    return SemanticTokenType.Number;

                case TokenKind.Generic:
                    return SemanticTokenType.Function;
            }

            // Default semantic token
            return SemanticTokenType.Documentation;
        }

        protected override Task<SemanticTokensDocument>
            GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(GetRegistrationOptions().Legend));
        }
    }
#pragma warning restore 618
}
