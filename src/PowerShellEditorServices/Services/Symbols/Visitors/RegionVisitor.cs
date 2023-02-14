// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    internal static class RegionVisitor
    {
        // This regular expression are used to match lines which mark the start of a region comment in a script.
        // Based on the defaults in the VS Code Language Configuration at;
        // https://github.com/Microsoft/vscode/blob/64186b0a26/extensions/powershell/language-configuration.json#L26-L31
        // https://github.com/Microsoft/vscode/issues/49070
        private static readonly Regex s_startRegionTextRegex = new(
            @"^\s*#[rR]egion\b", RegexOptions.Compiled);

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
                    s_startRegionTextRegex.IsMatch(token.Text))
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
