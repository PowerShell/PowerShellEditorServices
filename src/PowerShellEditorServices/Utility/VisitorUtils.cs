// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Text;
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
        /// <param name="nameStartIndex">An int specifying start index of name in the AST's extent text</param>
        /// <returns>A tuple with start column and line of the symbol name</returns>
        private static (int startColumn, int startLine) GetNameStartColumnAndLineFromAst(Ast ast, int nameStartIndex)
        {
            int startColumnNumber = ast.Extent.StartColumnNumber;
            int startLineNumber = ast.Extent.StartLineNumber;
            string astText = ast.Extent.Text;
            // astOffset is the offset on the entire text of the AST.
            for (int astOffset = 0; astOffset <= ast.Extent.Text.Length; astOffset++, startColumnNumber++)
            {
                if (astText[astOffset] == '\n')
                {
                    // reset numbers since we are operating on a different line and increment the line number.
                    startColumnNumber = 0;
                    startLineNumber++;
                }
                else if (astText[astOffset] == '\r')
                {
                    // Do nothing with carriage returns... we only look for line feeds since those
                    // are used on every platform.
                }
                else if (astOffset >= nameStartIndex && !char.IsWhiteSpace(astText[astOffset]))
                {
                    // This is the start of the function name so we've found our start column and line number.
                    break;
                }
            }

            return (startColumnNumber, startLineNumber);
        }

        /// <summary>
        /// Calculates the start line and column of the actual function name in a function definition AST.
        /// </summary>
        /// <param name="functionDefinitionAst">A FunctionDefinitionAst object in the script's AST</param>
        /// <returns>A tuple with start column and line for the function name</returns>
        internal static (int startColumn, int startLine) GetNameStartColumnAndLineFromAst(FunctionDefinitionAst functionDefinitionAst)
        {
            int startOffset = functionDefinitionAst.IsFilter ? "filter".Length : functionDefinitionAst.IsWorkflow ? "workflow".Length : "function".Length;
            return GetNameStartColumnAndLineFromAst(functionDefinitionAst, startOffset);
        }

        /// <summary>
        /// Calculates the start line and column of the actual class/enum name in a type definition AST.
        /// </summary>
        /// <param name="typeDefinitionAst">A TypeDefinitionAst object in the script's AST</param>
        /// <returns>A tuple with start column and line for the type name</returns>
        internal static (int startColumn, int startLine) GetNameStartColumnAndLineFromAst(TypeDefinitionAst typeDefinitionAst)
        {
            int startOffset = typeDefinitionAst.IsEnum ? "enum".Length : "class".Length;
            return GetNameStartColumnAndLineFromAst(typeDefinitionAst, startOffset);
        }

        /// <summary>
        /// Calculates the start line and column of the actual method/constructor name in a function member AST.
        /// </summary>
        /// <param name="functionMemberAst">A FunctionMemberAst object in the script's AST</param>
        /// <returns>A tuple with start column and line for the method/constructor name</returns>
        internal static (int startColumn, int startLine) GetNameStartColumnAndLineFromAst(FunctionMemberAst functionMemberAst)
        {
            // find name index to get offset even with attributes, static, hidden ++
            int nameStartIndex = functionMemberAst.Extent.Text.LastIndexOf(string.Concat(functionMemberAst.Name, '('), StringComparison.OrdinalIgnoreCase);
            return GetNameStartColumnAndLineFromAst(functionMemberAst, nameStartIndex);
        }

        /// <summary>
        /// Calculates the start line and column of the actual property name in a property member AST.
        /// </summary>
        /// <param name="propertyMemberAst">A PropertyMemberAst object in the script's AST</param>
        /// <param name="isEnumMember">A bool indicating this is a enum member</param>
        /// <returns>A tuple with start column and line for the property name</returns>
        internal static (int startColumn, int startLine) GetNameStartColumnAndLineFromAst(PropertyMemberAst propertyMemberAst, bool isEnumMember)
        {
            // find name index to get offset even with attributes, static, hidden ++
            string searchString = isEnumMember ? propertyMemberAst.Name : string.Concat('$', propertyMemberAst.Name);
            int nameStartIndex = propertyMemberAst.Extent.Text.LastIndexOf(searchString, StringComparison.OrdinalIgnoreCase);
            return GetNameStartColumnAndLineFromAst(propertyMemberAst, nameStartIndex);
        }

        /// <summary>
        /// Calculates the start line and column of the actual configuration name in a configuration definition AST.
        /// </summary>
        /// <param name="configurationDefinitionAst">A ConfigurationDefinitionAst object in the script's AST</param>
        /// <returns>A tuple with start column and line for the configuration name</returns>
        internal static (int startColumn, int startLine) GetNameStartColumnAndLineFromAst(ConfigurationDefinitionAst configurationDefinitionAst)
        {
            const int startOffset = 13; // "configuration".Length
            return GetNameStartColumnAndLineFromAst(configurationDefinitionAst, startOffset);
        }

        /// <summary>
        /// Gets a new ScriptExtent for a given Ast for the symbol name only (variable)
        /// </summary>
        /// <param name="functionDefinitionAst">A FunctionDefinitionAst in the script's AST</param>
        /// <returns>A ScriptExtent with for the symbol name only</returns>
        internal static PSESSymbols.ScriptExtent GetNameExtent(FunctionDefinitionAst functionDefinitionAst)
        {
            (int startColumn, int startLine) = GetNameStartColumnAndLineFromAst(functionDefinitionAst);

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
            (int startColumn, int startLine) = GetNameStartColumnAndLineFromAst(typeDefinitionAst);

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
        /// <param name="includeSignature">A bool indicating if return type and class should be included</param>
        /// <returns>A ScriptExtent with for the symbol name only</returns>
        internal static PSESSymbols.ScriptExtent GetNameExtent(FunctionMemberAst functionMemberAst, bool includeSignature = false)
        {
            (int startColumn, int startLine) = GetNameStartColumnAndLineFromAst(functionMemberAst);

            return new PSESSymbols.ScriptExtent()
            {
                Text = GetMemberOverloadName(functionMemberAst, includeSignature),
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
        /// <param name="includeSignature">A bool indicating if property-type and name of class/enum should be included</param>
        /// <returns>A ScriptExtent with for the symbol name only</returns>
        internal static PSESSymbols.ScriptExtent GetNameExtent(PropertyMemberAst propertyMemberAst, bool includeSignature = false)
        {
            bool isEnumMember = propertyMemberAst.Parent is TypeDefinitionAst typeDef && typeDef.IsEnum;
            (int startColumn, int startLine) = GetNameStartColumnAndLineFromAst(propertyMemberAst, isEnumMember);

            // +1 when class property to as start includes $
            int endColumnNumber = isEnumMember ?
                startColumn + propertyMemberAst.Name.Length :
                startColumn + propertyMemberAst.Name.Length + 1;

            return new PSESSymbols.ScriptExtent()
            {
                Text = GetMemberOverloadName(propertyMemberAst, includeSignature),
                StartLineNumber = startLine,
                EndLineNumber = startLine,
                StartColumnNumber = startColumn,
                EndColumnNumber = endColumnNumber,
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
            (int startColumn, int startLine) = GetNameStartColumnAndLineFromAst(configurationDefinitionAst);

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

        /// <summary>
        /// Gets the method or constructor name with parameters for current overload.
        /// </summary>
        /// <param name="functionMemberAst">A FunctionMemberAst object in the script's AST</param>
        /// <param name="includeSignature">A bool indicating if return type and class should be included</param>
        /// <returns>Function member name with return type (optional) and parameters</returns>
        internal static string GetMemberOverloadName(FunctionMemberAst functionMemberAst, bool includeSignature = false)
        {
            StringBuilder sb = new();

            // Prepend return type and class. Used for symbol details (hover)
            if (includeSignature)
            {
                if (!functionMemberAst.IsConstructor)
                {
                    sb.Append(functionMemberAst.ReturnType?.TypeName.Name ?? "void").Append(' ');
                }

                if (functionMemberAst.Parent is TypeDefinitionAst typeAst && typeAst.IsClass)
                {
                    sb.Append(typeAst.Name).Append('.');
                }
            }

            sb.Append(functionMemberAst.Name);

            // Add parameters
            sb.Append('(');
            if (functionMemberAst.Parameters.Count > 0)
            {
                List<string> parameters = new(functionMemberAst.Parameters.Count);
                foreach (ParameterAst param in functionMemberAst.Parameters)
                {
                    parameters.Add(param.Extent.Text);
                }

                sb.Append(string.Join(", ", parameters));
            }
            sb.Append(')');

            return sb.ToString();
        }

        /// <summary>
        /// Gets the property name with type and class/enum.
        /// </summary>
        /// <param name="propertyMemberAst">A PropertyMemberAst object in the script's AST</param>
        /// <param name="includeSignature">A bool indicating if property-type and name of class/enum should be included</param>
        /// <returns>Property name with type (optional) and class/enum</returns>
        internal static string GetMemberOverloadName(PropertyMemberAst propertyMemberAst, bool includeSignature = false)
        {
            StringBuilder sb = new();

            // Prepend return type and class. Used for symbol details (hover)
            if (includeSignature && propertyMemberAst.Parent is TypeDefinitionAst typeAst)
            {
                if (!typeAst.IsEnum)
                {
                    sb.Append(propertyMemberAst.PropertyType?.TypeName.Name ?? "object").Append(' ');
                }

                sb.Append(typeAst.Name).Append('.');
            }

            sb.Append(propertyMemberAst.Name);
            return sb.ToString();
        }
    }
}
