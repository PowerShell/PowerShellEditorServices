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
using System;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    //Disable warnings having to do with SemanticTokensHandler being labelled obsolete
#pragma warning disable 618
    internal class SemanticTokens : SemanticTokensHandler
    {
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;
        static readonly SemanticTokensRegistrationOptions _registrationOptions = new SemanticTokensRegistrationOptions() {
            DocumentSelector = DocumentSelector.ForLanguage("powershell"),
            Legend = new SemanticTokensLegend(),
            DocumentProvider = new Supports<SemanticTokensDocumentProviderOptions>(true,
                new SemanticTokensDocumentProviderOptions() {
                    Edits = true
                }),
            RangeProvider = true
        };

        public SemanticTokens(ILogger<SemanticTokens> logger, WorkspaceService workspaceService) : base(_registrationOptions)
        {
            _logger = logger;
            _workspaceService = workspaceService;
        }

        protected override Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier,
            CancellationToken cancellationToken)
        {
            ScriptFile file = _workspaceService.GetFile(DocumentUri.GetFileSystemPath(identifier));
            Token[] tokens = file.ScriptTokens;
            foreach (var token in tokens){
                PushToken(token, builder);
            }
            return Task.CompletedTask;
        }

        private static void PushToken(Token token, SemanticTokensBuilder builder)
        {
            if(token is StringExpandableToken stringExpandableToken)
            {
                // Try parsing tokens within the string
                if (stringExpandableToken.NestedTokens != null)
                {
                    foreach (Token t in stringExpandableToken.NestedTokens)
                    {
                        PushToken(t, builder);
                    }
                    return;
                }
            }

            //Tokens line and col numbers indexed starting from 1, expecting indexing from 0
            int line = token.Extent.StartLineNumber - 1;
            int index = token.Extent.StartColumnNumber - 1;

            builder.Push(line, index, token.Text.Length, MapSemanticToken(token), Array.Empty<string>());
        }

        private static SemanticTokenType MapSemanticToken(Token token)
        {
            // First check token flags
            if ((token.TokenFlags & TokenFlags.Keyword) != 0)
            {
                return SemanticTokenType.Keyword;
            }

            if ((token.TokenFlags & TokenFlags.CommandName) != 0)
            {
                return SemanticTokenType.Function;
            }

            if (token.Kind != TokenKind.Generic && (token.TokenFlags &
                (TokenFlags.BinaryOperator | TokenFlags.UnaryOperator | TokenFlags.AssignmentOperator)) != 0)
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

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
            ITextDocumentIdentifierParams @params,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(GetRegistrationOptions().Legend));
        }
    }
#pragma warning restore 618
}
