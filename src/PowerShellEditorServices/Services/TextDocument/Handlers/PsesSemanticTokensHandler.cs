// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesSemanticTokensHandler : SemanticTokensHandlerBase
    {
        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector,
            Legend = new SemanticTokensLegend(),
            Full = new SemanticTokensCapabilityRequestFull
            {
                Delta = true
            },
            Range = true
        };

        private readonly WorkspaceService _workspaceService;

        public PsesSemanticTokensHandler(WorkspaceService workspaceService) => _workspaceService = workspaceService;

        protected override Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier,
            CancellationToken cancellationToken)
        {
            ScriptFile file = _workspaceService.GetFile(identifier.TextDocument.Uri);
            foreach (Token token in file.ScriptTokens)
            {
                PushToken(token, builder);
            }
            return Task.CompletedTask;
        }

        private static void PushToken(Token token, SemanticTokensBuilder builder)
        {
            foreach (SemanticToken sToken in ConvertToSemanticTokens(token))
            {
                builder.Push(
                    sToken.Line,
                    sToken.Column,
                    length: sToken.Text.Length,
                    sToken.Type,
                    tokenModifiers: sToken.TokenModifiers);
            }
        }

        internal static IEnumerable<SemanticToken> ConvertToSemanticTokens(Token token)
        {
            if (token is StringExpandableToken stringExpandableToken)
            {
                // Try parsing tokens within the string
                if (stringExpandableToken.NestedTokens != null)
                {
                    foreach (Token t in stringExpandableToken.NestedTokens)
                    {
                        foreach (SemanticToken subToken in ConvertToSemanticTokens(t))
                        {
                            yield return subToken;
                        }
                    }
                    yield break;
                }
            }

            SemanticTokenType mappedType = MapSemanticTokenType(token);
            if (mappedType == default)
            {
                yield break;
            }

            //Note that both column and line numbers are 0-based
            yield return new SemanticToken(
                token.Text,
                mappedType,
                line: token.Extent.StartLineNumber - 1,
                column: token.Extent.StartColumnNumber - 1,
                tokenModifiers: Array.Empty<string>());
        }

        private static SemanticTokenType MapSemanticTokenType(Token token)
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

            if ((token.TokenFlags & TokenFlags.AttributeName) != 0)
            {
                return SemanticTokenType.Decorator;
            }

            if ((token.TokenFlags & TokenFlags.TypeName) != 0)
            {
                return SemanticTokenType.Type;
            }

            // This represents keys in hashtables and also properties like `Foo` in `$myVar.Foo`
            if ((token.TokenFlags & TokenFlags.MemberName) != 0)
            {
                return SemanticTokenType.Property;
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

                case TokenKind.Label:
                    return SemanticTokenType.Label;
            }

            return null;
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
            ITextDocumentIdentifierParams @params,
            CancellationToken cancellationToken) => Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
    }
}
