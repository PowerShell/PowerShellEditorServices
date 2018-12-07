//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Text.RegularExpressions;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides common operations for the tokens of a parsed script.
    /// </summary>
    internal static class TokenOperations
    {
        // Region kinds to align with VSCode's region kinds
        private const string RegionKindComment = "comment";
        private const string RegionKindRegion = "region";
        private const string RegionKindNone = null;

        // These regular expressions are used to match lines which mark the start and end of region comment in a PowerShell
        // script. They are based on the defaults in the VS Code Language Configuration at;
        // https://github.com/Microsoft/vscode/blob/64186b0a26/extensions/powershell/language-configuration.json#L26-L31
        static private readonly Regex s_startRegionTextRegex = new Regex(
           @"^\s*#region\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static private readonly Regex s_endRegionTextRegex = new Regex(
           @"^\s*#endregion\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Extracts all of the unique foldable regions in a script given the list tokens
        /// </summary>
        internal static FoldingReference[] FoldableRegions(
            Token[] tokens)
        {
            var foldableRegions = new List<FoldingReference>();
            var tokenCommentRegionStack = new Stack<Token>();
            Token blockStartToken = null;
            int blockNextLine = -1;

            for (int index = 0; index < tokens.Length; index++)
            {
                Token token = tokens[index];
                if (token.Kind != TokenKind.Comment) { continue; }
                // Processing for comment regions <# -> #>
                if (token.Extent.StartLineNumber != token.Extent.EndLineNumber)
                {
                    FoldingReference foldRef = CreateFoldingReference(token, token, RegionKindComment);
                    if (foldRef != null) { foldableRegions.Add(foldRef); }
                    continue;
                }

                if (!IsBlockComment(index, tokens)) { continue; }
                // Regex's are very expensive.  Use them sparingly!
                // Processing for # region -> # endregion
                if (s_startRegionTextRegex.IsMatch(token.Text))
                {
                    tokenCommentRegionStack.Push(token);
                    continue;
                }
                if (s_endRegionTextRegex.IsMatch(token.Text))
                {
                    // Mismatched regions in the script can cause bad stacks.
                    if (tokenCommentRegionStack.Count > 0)
                    {
                        FoldingReference foldRef = CreateFoldingReference(tokenCommentRegionStack.Pop(), token, RegionKindRegion);
                        if (foldRef != null) { foldableRegions.Add(foldRef); }
                    }
                    continue;
                }
                // If it's neither a start or end region then it could be block line comment
                // Processing for blocks of line comments # comment1\n# comment2\n...
                int thisLine = token.Extent.StartLineNumber - 1;
                if ((blockStartToken != null) && (thisLine != blockNextLine))
                {
                    FoldingReference foldRef = CreateFoldingReference(blockStartToken, blockNextLine - 1, RegionKindComment);
                    if (foldRef != null) { foldableRegions.Add(foldRef); }
                    blockStartToken = token;
                }
                if (blockStartToken == null) { blockStartToken = token; }
                blockNextLine = thisLine + 1;
            }
            // If we exit the token array and we're still processing comment lines, then the
            // comment block simply ends at the end of document
            if (blockStartToken != null)
            {
                FoldingReference foldRef = CreateFoldingReference(blockStartToken, blockNextLine - 1, RegionKindComment);
                if (foldRef != null) { foldableRegions.Add(foldRef); }
            }

            return foldableRegions.ToArray();
        }

        /// <summary>
        /// Creates an instance of a FoldingReference object from a start and end langauge Token
        /// Returns null if the line range is invalid
        /// </summary>
        static private FoldingReference CreateFoldingReference(
            Token startToken,
            Token endToken,
            string matchKind)
        {
            if (endToken.Extent.EndLineNumber == startToken.Extent.StartLineNumber) { return null; }
            // Extents are base 1, but LSP is base 0, so minus 1 off all lines and character positions
            return new FoldingReference {
                StartLine      = startToken.Extent.StartLineNumber - 1,
                StartCharacter = startToken.Extent.StartColumnNumber - 1,
                EndLine        = endToken.Extent.EndLineNumber - 1,
                EndCharacter   = endToken.Extent.EndColumnNumber - 1,
                Kind           = matchKind
            };
        }

        /// <summary>
        /// Creates an instance of a FoldingReference object from a start token and an end line
        /// Returns null if the line range is invalid
        /// </summary>
        static private FoldingReference CreateFoldingReference(
            Token startToken,
            int endLine,
            string matchKind)
        {
            if (endLine == (startToken.Extent.StartLineNumber - 1)) { return null; }
            // Extents are base 1, but LSP is base 0, so minus 1 off all lines and character positions
            return new FoldingReference {
                StartLine      = startToken.Extent.StartLineNumber - 1,
                StartCharacter = startToken.Extent.StartColumnNumber - 1,
                EndLine        = endLine,
                EndCharacter   = 0,
                Kind           = matchKind
            };
        }

        /// <summary>
        /// Returns true if a Token is a block comment;
        /// - Must be a TokenKind.comment
        /// - Must be preceeded by TokenKind.NewLine
        /// - Token text must start with a '#'.false  This is because comment regions
        ///   start with '&lt;#' but have the same TokenKind
        /// </summary>
        static private bool IsBlockComment(int index, Token[] tokens) {
            Token thisToken = tokens[index];
            if (thisToken.Kind != TokenKind.Comment) { return false; }
            if (index == 0) { return true; }
            if (tokens[index - 1].Kind != TokenKind.NewLine) { return false; }
            return thisToken.Text.StartsWith("#");
        }
    }
}
