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

        // Opening tokens for { } and @{ }
        private static readonly TokenKind[] s_openingBraces = new []
        {
            TokenKind.LCurly,
            TokenKind.AtCurly
        };

        // Opening tokens for ( ), @( ), $( )
        private static readonly TokenKind[] s_openingParens = new []
        {
            TokenKind.LParen,
            TokenKind.AtParen,
            TokenKind.DollarParen
        };

        /// <summary>
        /// Extracts all of the unique foldable regions in a script given the list tokens
        /// </summary>
        internal static FoldingReferenceList FoldableRegions(
            Token[] tokens)
        {
            var refList = new FoldingReferenceList();

            // Find matching braces  { -> }
            // Find matching hashes @{ -> }
            MatchTokenElements(tokens, s_openingBraces, TokenKind.RCurly, RegionKindNone, ref refList);

            // Find matching parentheses     ( -> )
            // Find matching array literals @( -> )
            // Find matching subexpressions $( -> )
            MatchTokenElements(tokens, s_openingParens, TokenKind.RParen, RegionKindNone, ref refList);

            // Find contiguous here strings @' -> '@
            MatchTokenElement(tokens, TokenKind.HereStringLiteral, RegionKindNone, ref refList);

            // Find unopinionated variable names ${ \n \n }
            MatchTokenElement(tokens, TokenKind.Variable, RegionKindNone, ref refList);

            // Find contiguous here strings @" -> "@
            MatchTokenElement(tokens, TokenKind.HereStringExpandable, RegionKindNone, ref refList);

            // Find matching comment regions   #region -> #endregion
            MatchCustomCommentRegionTokenElements(tokens, RegionKindRegion, ref refList);

            // Find blocks of line comments # comment1\n# comment2\n...
            MatchBlockCommentTokenElement(tokens, RegionKindComment, ref refList);

            // Find comments regions <# -> #>
            MatchTokenElement(tokens, TokenKind.Comment, RegionKindComment, ref refList);

            return refList;
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
        /// Given an array of tokens, find matching regions which start (array of tokens) and end with a different TokenKind
        /// </summary>
        static private void MatchTokenElements(
            Token[] tokens,
            TokenKind[] startTokenKind,
            TokenKind endTokenKind,
            string matchKind,
            ref FoldingReferenceList refList)
        {
            Stack<Token> tokenStack = new Stack<Token>();
            foreach (Token token in tokens)
            {
                if (Array.IndexOf(startTokenKind, token.Kind) != -1) {
                    tokenStack.Push(token);
                }
                if ((tokenStack.Count > 0) && (token.Kind == endTokenKind)) {
                    refList.SafeAdd(CreateFoldingReference(tokenStack.Pop(), token, matchKind));
                }
            }
        }

        /// <summary>
        /// Given an array of token, finds a specific token
        /// </summary>
        static private void MatchTokenElement(
            Token[] tokens,
            TokenKind tokenKind,
            string matchKind,
            ref FoldingReferenceList refList)
        {
            foreach (Token token in tokens)
            {
                if ((token.Kind == tokenKind) && (token.Extent.StartLineNumber != token.Extent.EndLineNumber)) {
                    refList.SafeAdd(CreateFoldingReference(token, token, matchKind));
                }
            }
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
        static private void MatchBlockCommentTokenElement(
            Token[] tokens,
            string matchKind,
             ref FoldingReferenceList refList)
        {
            Token startToken = null;
            int nextLine = -1;
            for (int index = 0; index < tokens.Length; index++)
            {
                Token thisToken = tokens[index];
                if (IsBlockComment(index, tokens) && s_nonRegionLineCommentRegex.IsMatch(thisToken.Text)) {
                    int thisLine = thisToken.Extent.StartLineNumber - 1;
                    if ((startToken != null) && (thisLine != nextLine)) {
                        refList.SafeAdd(CreateFoldingReference(startToken, nextLine - 1, matchKind));
                        startToken = thisToken;
                    }
                    if (startToken == null) { startToken = thisToken; }
                    nextLine = thisLine + 1;
                }
            }
            // If we exit the token array and we're still processing comment lines, then the
            // comment block simply ends at the end of document
            if (startToken != null) {
                refList.SafeAdd(CreateFoldingReference(startToken, nextLine - 1, matchKind));
            }
        }

        /// <summary>
        /// Given a list of tokens, find the tokens that are comments and
        /// the comment text is either `# region` or `# endregion`, and then use a stack to determine
        /// the ranges they span
        /// </summary>
        static private void MatchCustomCommentRegionTokenElements(
            Token[] tokens,
            string matchKind,
            ref FoldingReferenceList refList)
        {
            // These regular expressions are used to match lines which mark the start and end of region comment in a PowerShell
            // script. They are based on the defaults in the VS Code Language Configuration at;
            // https://github.com/Microsoft/vscode/blob/64186b0a26/extensions/powershell/language-configuration.json#L26-L31
            string startRegionText = @"^\s*#region\b";
            string endRegionText = @"^\s*#endregion\b";

            Stack<Token> tokenStack = new Stack<Token>();
            for (int index = 0; index < tokens.Length; index++)
            {
                if (IsBlockComment(index, tokens)) {
                    Token token = tokens[index];
                    if (Regex.IsMatch(token.Text, startRegionText, RegexOptions.IgnoreCase)) {
                        tokenStack.Push(token);
                    }
                    if ((tokenStack.Count > 0) && (Regex.IsMatch(token.Text, endRegionText, RegexOptions.IgnoreCase))) {
                        refList.SafeAdd(CreateFoldingReference(tokenStack.Pop(), token, matchKind));
                    }
                }
            }
        }
    }
}
