// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation.Language;
using PSESSymbols = Microsoft.PowerShell.EditorServices.Services.Symbols;

namespace Microsoft.PowerShell.EditorServices.Utility
{
    /// <summary>
    /// General common utilities for AST visitors to prevent reimplementation.
    /// </summary>
    internal static class VisitorUtils
    {
        /// <summary>
        /// Calculates the start line and column of the actual symbol name in a AST.
        /// </summary>
        /// <param name="ast">An Ast object in the script's AST</param>
        /// <param name="firstLineColumnOffset">An offset specifying where to begin searching in the first line of the AST's extent text</param>
        /// <returns>A tuple with start column and line of the symbol name</returns>
        private static (int startColumn, int startLine) GetNameStartColumnAndLineNumbersFromAst(Ast ast, int firstLineColumnOffset)
        {
            int startColumnNumber = ast.Extent.StartColumnNumber;
            int startLineNumber = ast.Extent.StartLineNumber;
            int astOffset = firstLineColumnOffset;
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

        /// <summary>
        /// Gets a new ScriptExtent for a given Ast for the symbol name only (variable)
        /// </summary>
        /// <param name="functionDefinitionAst">A FunctionDefinitionAst in the script's AST</param>
        /// <returns>A ScriptExtent with for the symbol name only</returns>
        internal static PSESSymbols.ScriptExtent GetNameExtent(FunctionDefinitionAst functionDefinitionAst)
        {
            int astOffset = functionDefinitionAst.IsFilter ? "filter".Length : functionDefinitionAst.IsWorkflow ? "workflow".Length : "function".Length;
            (int startColumn, int startLine) = GetNameStartColumnAndLineNumbersFromAst(functionDefinitionAst, astOffset);

            return new PSESSymbols.ScriptExtent()
            {
                Text = functionDefinitionAst.Name,
                StartLineNumber = startLine,
                EndLineNumber = startLine,
                StartColumnNumber = startColumn,
                EndColumnNumber = startColumn + functionDefinitionAst.Name.Length,
                File = functionDefinitionAst.Extent.File
            };
        }

        /// <summary>
        /// Gets a new ScriptExtent for a given Ast for the symbol name only (variable)
        /// </summary>
        /// <param name="typeDefinitionAst">A TypeDefinitionAst in the script's AST</param>
        /// <returns>A ScriptExtent with for the symbol name only</returns>
        internal static PSESSymbols.ScriptExtent GetNameExtent(TypeDefinitionAst typeDefinitionAst)
        {
            int astOffset = typeDefinitionAst.IsEnum ? "enum".Length : "class".Length;
            (int startColumn, int startLine) = GetNameStartColumnAndLineNumbersFromAst(typeDefinitionAst, astOffset);

            return new PSESSymbols.ScriptExtent()
            {
                Text = typeDefinitionAst.Name,
                StartLineNumber = startLine,
                EndLineNumber = startLine,
                StartColumnNumber = startColumn,
                EndColumnNumber = startColumn + typeDefinitionAst.Name.Length,
                File = typeDefinitionAst.Extent.File
            };
        }

        /// <summary>
        /// Gets a new ScriptExtent for a given Ast for the symbol name only (variable)
        /// </summary>
        /// <param name="functionMemberAst">A FunctionMemberAst in the script's AST</param>
        /// <returns>A ScriptExtent with for the symbol name only</returns>
        internal static PSESSymbols.ScriptExtent GetNameExtent(FunctionMemberAst functionMemberAst)
        {
            // offset by [type] if return type is specified
            int astOffset = functionMemberAst.ReturnType?.Extent.Text.Length ?? 0;
            (int startColumn, int startLine) = GetNameStartColumnAndLineNumbersFromAst(functionMemberAst, astOffset);

            return new PSESSymbols.ScriptExtent()
            {
                Text = functionMemberAst.Name,
                StartLineNumber = startLine,
                EndLineNumber = startLine,
                StartColumnNumber = startColumn,
                EndColumnNumber = startColumn + functionMemberAst.Name.Length,
                File = functionMemberAst.Extent.File
            };
        }

        /// <summary>
        /// Gets a new ScriptExtent for a given Ast for the property name only
        /// </summary>
        /// <param name="propertyMemberAst">A PropertyMemberAst in the script's AST</param>
        /// <returns>A ScriptExtent with for the symbol name only</returns>
        internal static PSESSymbols.ScriptExtent GetNameExtent(PropertyMemberAst propertyMemberAst)
        {
            // offset by [type] if type is specified
            int astOffset = propertyMemberAst.PropertyType?.Extent.Text.Length ?? 0;
            (int startColumn, int startLine) = GetNameStartColumnAndLineNumbersFromAst(propertyMemberAst, astOffset);

            return new PSESSymbols.ScriptExtent()
            {
                Text = propertyMemberAst.Name,
                StartLineNumber = startLine,
                EndLineNumber = startLine,
                StartColumnNumber = startColumn,
                EndColumnNumber = startColumn + propertyMemberAst.Name.Length + 1,
                File = propertyMemberAst.Extent.File
            };
        }

        /// <summary>
        /// Gets a new ScriptExtent for a given Ast for the configuration instance name only
        /// </summary>
        /// <param name="configurationDefinitionAst">A ConfigurationDefinitionAst in the script's AST</param>
        /// <returns>A ScriptExtent with for the symbol name only</returns>
        internal static PSESSymbols.ScriptExtent GetNameExtent(ConfigurationDefinitionAst configurationDefinitionAst)
        {
            string configurationName = configurationDefinitionAst.InstanceName.Extent.Text;
            const int astOffset = 13; // "configuration".Length
            (int startColumn, int startLine) = GetNameStartColumnAndLineNumbersFromAst(configurationDefinitionAst, astOffset);

            return new PSESSymbols.ScriptExtent()
            {
                Text = configurationName,
                StartLineNumber = startLine,
                EndLineNumber = startLine,
                StartColumnNumber = startColumn,
                EndColumnNumber = startColumn + configurationName.Length,
                File = configurationDefinitionAst.Extent.File
            };
        }
    }
}
