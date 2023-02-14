// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    internal static class RegionVisitor
    {
        internal static void FindRegionsInDocument(ScriptFile file, Func<SymbolReference, AstVisitAction> action)
        {
            Token[] tokens = file.ScriptTokens;
            for (int i = 0; i < tokens.Length; i++)
            {
                Token token = tokens[i];

                // Exclude everything but single-line comments
                if (token.Kind != TokenKind.Comment ||
                    token.Extent.StartLineNumber != token.Extent.EndLineNumber)
                {
                    continue;
                }

                // Look for <newline> #region <optional name>
                // Document symbols only care about the symbol start and regex is expensive,
                // so skip checking if region is actually closed with #endregion.
                if (TokenOperations.IsBlockComment(i, tokens) &&
                    TokenOperations.s_startRegionTextRegex.IsMatch(token.Text))
                {
                    action(new SymbolReference(
                            SymbolType.Region,
                            token.Extent.Text.TrimStart().TrimStart('#'),
                            token.Extent.Text,
                            token.Extent,
                            token.Extent,
                            file,
                            isDeclaration: true));
                }
            }
        }
    }
}
