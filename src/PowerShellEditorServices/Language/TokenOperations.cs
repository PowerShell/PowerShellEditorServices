//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
        private const string RegionKindComment = "comment";
        private const string RegionKindRegion = "region";
        private const string RegionKindNone = null;

        /// <summary>
        /// Extracts all of the unique foldable regions in a script given the list tokens
        /// </summary>
        internal static FoldingReference[] FoldableRegions(Token[] tokens) {
            List<FoldingReference> foldableRegions = new List<FoldingReference>();

            // Find matching braces { -> }
            foldableRegions.AddRange(
                MatchTokenElements(tokens, TokenKind.LCurly, TokenKind.RCurly, RegionKindNone)
            );

            // Find matching braces ( -> )
            foldableRegions.AddRange(
                MatchTokenElements(tokens, TokenKind.LParen, TokenKind.RParen, RegionKindNone)
            );

            // Find matching arrays @( -> )
            foldableRegions.AddRange(
                MatchTokenElements(tokens, TokenKind.AtParen, TokenKind.RParen, RegionKindNone)
            );

            // Find matching hashes @{ -> }
            foldableRegions.AddRange(
                MatchTokenElements(tokens, TokenKind.AtCurly, TokenKind.RParen, RegionKindNone)
            );

            // Find contiguous here strings @' -> '@
            foldableRegions.AddRange(
                MatchTokenElement(tokens, TokenKind.HereStringLiteral, RegionKindNone)
            );

            // Find contiguous here strings @" -> "@
            foldableRegions.AddRange(
                MatchTokenElement(tokens, TokenKind.HereStringExpandable, RegionKindNone)
            );

            // Find matching comment regions   #region -> #endregion
            foldableRegions.AddRange(
                MatchCustomCommentRegionTokenElements(tokens, RegionKindRegion)
            );

            // Find blocks of line comments # comment1\n# comment2\n...
            foldableRegions.AddRange(
                MatchBlockCommentTokenElement(tokens, RegionKindComment)
            );

            // Find comments regions <# -> #>
            foldableRegions.AddRange(
                MatchTokenElement(tokens, TokenKind.Comment, RegionKindComment)
            );

            // Remove any null entries. Nulls appear if the folding reference is invalid
            // or missing
            foldableRegions.RemoveAll(item => item == null);

            // Sort the FoldingReferences, starting at the top of the document,
            // and ensure that, in the case of multiple ranges starting the same line,
            // that the largest range (i.e. most number of lines spanned) is sorted
            // first. This is needed to detect duplicate regions. The first in the list
            // will be used and subsequent duplicates ignored.
            foldableRegions.Sort();

            // It's possible to have duplicate or overlapping ranges, that is, regions which have the same starting
            // line number as the previous region. Therefore only emit ranges which have a different starting line
            // than the previous range.
            foldableRegions.RemoveAll( (FoldingReference item) => {
                // Note - I'm not happy with searching here, but as the RemoveAll
                // doesn't expose the index in the List, we need to calculate it. Fortunately the
                // list is sorted at this point, so we can use BinarySearch.
                int index = foldableRegions.BinarySearch(item);
                if (index == 0) { return false; }
                return (item.StartLine == foldableRegions[index - 1].StartLine);
            });

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
        /// Given an array of tokens, find matching regions which start and end with a different TokenKind
        /// </summary>
        static private List<FoldingReference> MatchTokenElements(
            Token[] tokens,
            TokenKind startTokenKind,
            TokenKind endTokenKind,
            string matchKind)
        {
            List<FoldingReference> result = new List<FoldingReference>();
            Stack<Token> tokenStack = new Stack<Token>();
            foreach (Token token in tokens)
            {
                if (token.Kind == startTokenKind) {
                    tokenStack.Push(token);
                }
                if ((tokenStack.Count > 0) && (token.Kind == endTokenKind)) {
                    result.Add(CreateFoldingReference(tokenStack.Pop(), token, matchKind));
                }
            }
            return result;
        }

        /// <summary>
        /// Given an array of token, finds a specific token
        /// </summary>
        static private List<FoldingReference> MatchTokenElement(
            Token[] tokens,
            TokenKind tokenKind,
            string matchKind)
        {
            List<FoldingReference> result = new List<FoldingReference>();
            foreach (Token token in tokens)
            {
                if ((token.Kind == tokenKind) && (token.Extent.StartLineNumber != token.Extent.EndLineNumber)) {
                    result.Add(CreateFoldingReference(token, token, matchKind));
                }
            }
            return result;
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

        // This regular expressions is used to detect a line comment (as opposed to an inline comment), that is not a region
        // block directive i.e.
        // - No text between the beginning of the line and `#`
        // - Comment does start with region
        // - Comment does start with endregion
        static private readonly Regex s_nonRegionLineCommentRegex = new Regex(
            @"\s*#(?!region\b|endregion\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Finding blocks of comment tokens is more complicated as the newline characters are not
        /// classed as comments.  To workaround this we search for valid block comments (See IsBlockCmment)
        /// and then determine contiguous line numbers from there
        /// </summary>
        static private List<FoldingReference> MatchBlockCommentTokenElement(
            Token[] tokens,
            string matchKind)
        {
            List<FoldingReference> result = new List<FoldingReference>();
            Token startToken = null;
            int nextLine = -1;
            for (int index = 0; index < tokens.Length; index++)
            {
                Token thisToken = tokens[index];
                if (IsBlockComment(index, tokens) && s_nonRegionLineCommentRegex.IsMatch(thisToken.Text)) {
                    int thisLine = thisToken.Extent.StartLineNumber - 1;
                    if ((startToken != null) && (thisLine != nextLine)) {
                        result.Add(CreateFoldingReference(startToken, nextLine - 1, matchKind));
                        startToken = thisToken;
                    }
                    if (startToken == null) { startToken = thisToken; }
                    nextLine = thisLine + 1;
                }
            }
            // If we exit the token array and we're still processing comment lines, then the
            // comment block simply ends at the end of document
            if (startToken != null) {
                result.Add(CreateFoldingReference(startToken, nextLine - 1, matchKind));
            }
            return result;
        }

        /// <summary>
        /// Given a list of tokens, find the tokens that are comments and
        /// the comment text is either `# region` or `# endregion`, and then use a stack to determine
        /// the ranges they span
        /// </summary>
        static private List<FoldingReference> MatchCustomCommentRegionTokenElements(
            Token[] tokens,
            string matchKind)
        {
            // These regular expressions are used to match lines which mark the start and end of region comment in a PowerShell
            // script. They are based on the defaults in the VS Code Language Configuration at;
            // https://github.com/Microsoft/vscode/blob/64186b0a26/extensions/powershell/language-configuration.json#L26-L31
            string startRegionText = @"^\s*#region\b";
            string endRegionText = @"^\s*#endregion\b";

            List<FoldingReference> result = new List<FoldingReference>();
            Stack<Token> tokenStack = new Stack<Token>();
            for (int index = 0; index < tokens.Length; index++)
            {
                if (IsBlockComment(index, tokens)) {
                    Token token = tokens[index];
                    if (Regex.IsMatch(token.Text, startRegionText, RegexOptions.IgnoreCase)) {
                        tokenStack.Push(token);
                    }
                    if ((tokenStack.Count > 0) && (Regex.IsMatch(token.Text, endRegionText, RegexOptions.IgnoreCase))) {
                        result.Add(CreateFoldingReference(tokenStack.Pop(), token, matchKind));
                    }
                }
            }
            return result;
        }
    }
}
