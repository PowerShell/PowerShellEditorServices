//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
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
        /// <returns>A decision to stop searching if the right FunctionDefinitionAst was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            // Get the start column number of the function name,
            // instead of the the start column of 'function' and create new extent for the functionName
            int startColumnNumber =
                functionDefinitionAst.Extent.Text.IndexOf(
                    functionDefinitionAst.Name, StringComparison.OrdinalIgnoreCase) + 1;

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = functionDefinitionAst.Name,
                StartLineNumber = functionDefinitionAst.Extent.StartLineNumber,
                StartColumnNumber = startColumnNumber,
                EndLineNumber = functionDefinitionAst.Extent.StartLineNumber,
                EndColumnNumber = startColumnNumber + functionDefinitionAst.Name.Length,
                File = functionDefinitionAst.Extent.File
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

            // We want to check VariableExpressionAsts from within this AssignmentStatementAst so we visit it.
            FindDeclarationVariableExpressionVisitor visitor = new FindDeclarationVariableExpressionVisitor(symbolRef);
            assignmentStatementAst.Left.Visit(visitor);

            if (visitor.FoundDeclaration != null)
            {
                FoundDeclaration = visitor.FoundDeclaration;
                return AstVisitAction.StopVisit;
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// The private visitor used to find the variable expression that matches a symbol
        /// </summary>
        private class FindDeclarationVariableExpressionVisitor : AstVisitor
        {
            private SymbolReference symbolRef;
            private string variableName;

            public SymbolReference FoundDeclaration{ get; private set; }

            public FindDeclarationVariableExpressionVisitor(SymbolReference symbolRef)
            {
                this.symbolRef = symbolRef;
                if (this.symbolRef.SymbolType == SymbolType.Variable)
                {
                    // converts `$varName` to `varName` or of the form ${varName} to varName
                    variableName = symbolRef.SymbolName.TrimStart('$').Trim('{', '}');
                }
            }

            /// <summary>
            /// Check if the VariableExpressionAst has the same name as that of symbolRef.
            /// </summary>
            /// <param name="variableExpressionAst">A VariableExpressionAst</param>
            /// <returns>A decision to stop searching if the right VariableExpressionAst was found,
            /// or a decision to continue if it wasn't found</returns>
            public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
            {
                if (variableExpressionAst.VariablePath.UserPath.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                {
                    // TODO also find instances of set-variable
                    FoundDeclaration = new SymbolReference(SymbolType.Variable, variableExpressionAst.Extent);
                    return AstVisitAction.StopVisit;
                }
                return AstVisitAction.Continue;
            }

            public override AstVisitAction VisitMemberExpression(MemberExpressionAst functionDefinitionAst)
            {
                // We don't want to discover any variables in member expressisons (`$something.Foo`)
                return AstVisitAction.SkipChildren;
            }

            public override AstVisitAction VisitIndexExpression(IndexExpressionAst functionDefinitionAst)
            {
                // We don't want to discover any variables in index expressions (`$something[0]`)
                return AstVisitAction.SkipChildren;
            }
        }
    }
}
