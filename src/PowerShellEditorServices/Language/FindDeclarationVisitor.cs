//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// The visitor used to find the definition of a symbol
    /// </summary>
    internal class FindDeclarationVisitor : AstVisitor
    {
        private SymbolReference symbolRef;
        private string variableName;

        public SymbolReference FoundDeclaration{ get; private set; }

        public FindDeclarationVisitor(SymbolReference symbolRef)
        {
            this.symbolRef = symbolRef;
            if (this.symbolRef.SymbolType == SymbolType.Variable)
            {
                // converts `$varName` to `varName` or of the form ${varName} to varName
                variableName = symbolRef.SymbolName.TrimStart('$').Trim('{', '}');
            }
        }

        /// <summary>
        /// Decides if the current function definition is the right definition
        /// for the symbol being searched for. The definition of the symbol will be a of type
        /// SymbolType.Function and have the same name as the symbol
        /// </summary>
        /// <param name="functionDefinitionAst">A FunctionDefinitionAst in the script's AST</param>
        /// <returns>A descion to stop searching if the right FunctionDefinitionAst was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            // Get the start column number of the function name,
            // instead of the the start column of 'function' and create new extent for the functionName
            int startColumnNumber =
                functionDefinitionAst.Extent.Text.IndexOf(
                    functionDefinitionAst.Name) + 1;

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = functionDefinitionAst.Name,
                StartLineNumber = functionDefinitionAst.Extent.StartLineNumber,
                StartColumnNumber = startColumnNumber,
                EndLineNumber = functionDefinitionAst.Extent.StartLineNumber,
                EndColumnNumber = startColumnNumber + functionDefinitionAst.Name.Length
            };

            if (symbolRef.SymbolType.Equals(SymbolType.Function) &&
                 nameExtent.Text.Equals(symbolRef.ScriptRegion.Text, StringComparison.CurrentCultureIgnoreCase))
            {
                this.FoundDeclaration =
                    new SymbolReference(
                        SymbolType.Function,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return base.VisitFunctionDefinition(functionDefinitionAst);
        }

        /// <summary>
        /// Check if the left hand side of an assignmentStatementAst is a VariableExpressionAst
        /// with the same name as that of symbolRef.
        /// </summary>
        /// <param name="assignmentStatementAst">An AssignmentStatementAst</param>
        /// <returns>A decision to stop searching if the right VariableExpressionAst was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitAssignmentStatement(AssignmentStatementAst assignmentStatementAst)
        {
            if (variableName == null)
            {
                return AstVisitAction.Continue;
            }

            // The AssignmentStatementAst could contain either of the following Ast types:
            // VariableExpressionAst, ArrayLiteralAst, ConvertExpressionAst, AttributedExpressionAst
            // We might need to recurse down the tree to find the VariableExpressionAst we're looking for
            List<VariableExpressionAst> asts = FindMatchingAsts(assignmentStatementAst.Left, variableName);
            if (asts.Count > 0)
            {
                FoundDeclaration = new SymbolReference(SymbolType.Variable, asts.FirstOrDefault().Extent);
                return AstVisitAction.StopVisit;
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Takes in an ExpressionAst and recurses until it finds all VariableExpressionAst and returns the list of them.
        /// We expect this ExpressionAst to be either: VariableExpressionAst, ArrayLiteralAst, ConvertExpressionAst, AttributedExpressionAst
        /// </summary>
        /// <param name="ast">An ExpressionAst</param>
        /// <param name="variableName">The name of the variable we are trying to find</param>
        /// <returns>A list of ExpressionAsts that match the variable name provided</returns>
        private static List<VariableExpressionAst> FindMatchingAsts (ExpressionAst ast, string variableName) {

            // VaraibleExpressionAst case - aka base case. This will return a list with the variableExpressionAst in it or an empty list if it's not the right one.
            var variableExprAst = ast as VariableExpressionAst;
            if (variableExprAst != null)
            {
                if (variableExprAst.VariablePath.UserPath.Equals(
                    variableName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new List<VariableExpressionAst>{ variableExprAst };
                }
                return new List<VariableExpressionAst>();
            }

            // VariableExpressionAsts could be an element of an ArrayLiteralAst. This adds all the elements to the call stack.
            var arrayLiteralAst = ast as ArrayLiteralAst;
            if (arrayLiteralAst != null)
            {
                return (arrayLiteralAst.Elements.SelectMany(e => FindMatchingAsts(e, variableName)).ToList());
            }

            // The ConvertExpressionAst (static casting for example `[string]$foo = "asdf"`) could contain a VariableExpressionAst so we recurse down
            var convertExprAst = ast as ConvertExpressionAst;
            if (convertExprAst != null && convertExprAst.Child != null)
            {
                return FindMatchingAsts(convertExprAst.Child, variableName);
            }

            // The AttributedExpressionAst (any attribute for example `[NotNull()]$foo = "asdf"`) could contain a VariableExpressionAst so we recurse down
            var attributedExprAst = ast as AttributedExpressionAst;
            if (attributedExprAst != null && attributedExprAst.Child != null)
            {
                return FindMatchingAsts(attributedExprAst.Child, variableName);
            };

            // We shouldn't ever get here, but in case a new Ast type is added to PowerShell, this fails gracefully
            return new List<VariableExpressionAst>();
        }
    }
}
