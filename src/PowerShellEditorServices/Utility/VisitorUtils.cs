// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// General common utilities for AST visitors to prevent reimplementation.
    /// </summary>
    internal static class VisitorUtils
    {
        /// <summary>
        /// Calculates the start line and column of the actual function name in a function definition AST.
        /// </summary>
        /// <param name="ast">A FunctionDefinitionAst object in the script's AST</param>
        /// <returns>A tuple with start column and line for the function name</returns>
        internal static (int startColumn, int startLine) GetNameStartColumnAndLineNumbersFromAst(FunctionDefinitionAst ast)
        {
            int startColumnNumber = ast.Extent.StartColumnNumber;
            int startLineNumber = ast.Extent.StartLineNumber;
            int astOffset = ast.IsFilter ? "filter".Length : ast.IsWorkflow ? "workflow".Length : "function".Length;
            string astText = ast.Extent.Text;
            // The line offset represents the offset on the line that we're on where as
            // astOffset is the offset on the entire text of the AST.
            int lineOffset = astOffset;
            for (; astOffset < astText.Length; astOffset++, lineOffset++)
            {
                if (astText[astOffset] == '\n')
                {
                    // reset numbers since we are operating on a different line and increment the line number.
                    startColumnNumber = 0;
                    startLineNumber++;
                    lineOffset = 0;
                }
                else if (astText[astOffset] == '\r')
                {
                    // Do nothing with carriage returns... we only look for line feeds since those
                    // are used on every platform.
                }
                else if (!char.IsWhiteSpace(astText[astOffset]))
                {
                    // This is the start of the function name so we've found our start column and line number.
                    break;
                }
            }

            return (startColumnNumber + lineOffset, startLineNumber);
        }
    }
}
