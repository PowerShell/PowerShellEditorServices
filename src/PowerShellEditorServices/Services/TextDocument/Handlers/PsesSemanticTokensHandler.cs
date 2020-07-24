//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document.Proposals;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesSemanticTokensHandler : SemanticTokensHandler
    {
        private static readonly SemanticTokensRegistrationOptions s_registrationOptions = new SemanticTokensRegistrationOptions
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector,
            Legend = new SemanticTokensLegend(),
            DocumentProvider = new Supports<SemanticTokensDocumentProviderOptions>(
                isSupported: true,
                new SemanticTokensDocumentProviderOptions
                {
                    Edits = true
                }),
            RangeProvider = true
        };
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        public PsesSemanticTokensHandler(ILogger<PsesSemanticTokensHandler> logger, WorkspaceService workspaceService) : base(s_registrationOptions)
        {
            _logger = logger;
            _workspaceService = workspaceService;
        }

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
                    sToken.Index,
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
                            yield return subToken;
                    }
                    yield break;
                }
            }

            SemanticTokenType mappedType = MapSemanticTokenType(token);
            if (mappedType == null)
            {
                yield break;
            }

            //Tokens line and col numbers indexed starting from 1, expecting indexing from 0
            int line = token.Extent.StartLineNumber - 1;
            int index = token.Extent.StartColumnNumber - 1;
            SemanticToken sToken = new SemanticToken(token.Text, mappedType,
                line, index, Array.Empty<string>());
            yield return sToken;
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

            return null;
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
            ITextDocumentIdentifierParams @params,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(GetRegistrationOptions().Legend));
        }
    }
}
