// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    internal static class RegionVisitor
    {
        internal static void GetRegionsInDocument(ScriptFile file, Func<SymbolReference, AstVisitAction> action)
        {
            Stack<Token> tokenCommentRegionStack = new();
            Token[] tokens = file.ScriptTokens;

            for (int i = 0; i < tokens.Length; i++)
            {
                Token token = tokens[i];

                // Exclude everything but single-line comments
                if (token.Kind != TokenKind.Comment ||
                    token.Extent.StartLineNumber != token.Extent.EndLineNumber ||
                    !TokenOperations.IsBlockComment(i, tokens))
                {
                    continue;
                }

                // Processing for #region -> #endregion
                if (TokenOperations.s_startRegionTextRegex.IsMatch(token.Text))
                {
                    tokenCommentRegionStack.Push(token);
                    continue;
                }

                if (TokenOperations.s_endRegionTextRegex.IsMatch(token.Text))
                {
                    // Mismatched regions in the script can cause bad stacks.
                    if (tokenCommentRegionStack.Count > 0)
                    {
                        Token regionStart = tokenCommentRegionStack.Pop();
                        Token regionEnd = token;

                        BufferRange regionRange = new(
                            regionStart.Extent.StartLineNumber,
                            regionStart.Extent.StartColumnNumber,
                            regionEnd.Extent.EndLineNumber,
                            regionEnd.Extent.EndColumnNumber);

                        action(new SymbolReference(
                            SymbolType.Region,
                            regionStart.Extent.Text.TrimStart().TrimStart('#'),
                            regionStart.Extent.Text,
                            regionStart.Extent,
                            new ScriptExtent()
                            {
                                Text = string.Join(Environment.NewLine, file.GetLinesInRange(regionRange)),
                                StartLineNumber = regionStart.Extent.StartLineNumber,
                                StartColumnNumber = regionStart.Extent.StartColumnNumber,
                                StartOffset = regionStart.Extent.StartOffset,
                                EndLineNumber = regionEnd.Extent.EndLineNumber,
                                EndColumnNumber = regionEnd.Extent.EndColumnNumber,
                                EndOffset = regionEnd.Extent.EndOffset,
                                File = regionStart.Extent.File
                            },
                            file,
                            isDeclaration: true));
                    }
                }
            }
        }
    }
}
