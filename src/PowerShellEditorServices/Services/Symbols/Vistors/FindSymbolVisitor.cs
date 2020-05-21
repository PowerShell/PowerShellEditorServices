//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The visitor used to find the the symbol at a specfic location in the AST
    /// </summary>
    internal class FindSymbolVisitor : AstVisitor
    {
        private int lineNumber;
        private int columnNumber;
        private bool includeFunctionDefinitions;

        public SymbolReference FoundSymbolReference { get; private set; }

        public FindSymbolVisitor(
            int lineNumber,
            int columnNumber,
            bool includeFunctionDefinitions)
        {
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
            this.includeFunctionDefinitions = includeFunctionDefinitions;
        }

        /// <summary>
        /// Checks to see if this command ast is the symbol we are looking for.
        /// </summary>
        /// <param name="commandAst">A CommandAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            Ast commandNameAst = commandAst.CommandElements[0];

            if (this.IsPositionInExtent(commandNameAst.Extent))
            {
                this.FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Function,
                        commandNameAst.Extent);

                return AstVisitAction.StopVisit;
            }

            return base.VisitCommand(commandAst);
        }

        /// <summary>
        /// Checks to see if this function definition is the symbol we are looking for.
        /// </summary>
        /// <param name="functionDefinitionAst">A functionDefinitionAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            int startColumnNumber = 1;

            if (!this.includeFunctionDefinitions)
            {
                startColumnNumber =
                    functionDefinitionAst.Extent.Text.IndexOf(
                        functionDefinitionAst.Name) + 1;
            }

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = functionDefinitionAst.Name,
                StartLineNumber = functionDefinitionAst.Extent.StartLineNumber,
                EndLineNumber = functionDefinitionAst.Extent.EndLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = startColumnNumber + functionDefinitionAst.Name.Length,
                File = functionDefinitionAst.Extent.File
            };

            if (this.IsPositionInExtent(nameExtent))
            {
                this.FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Function,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return base.VisitFunctionDefinition(functionDefinitionAst);
        }

        /// <summary>
        /// Checks to see if this command parameter is the symbol we are looking for.
        /// </summary>
        /// <param name="commandParameterAst">A CommandParameterAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitCommandParameter(CommandParameterAst commandParameterAst)
        {
            if (this.IsPositionInExtent(commandParameterAst.Extent))
            {
                this.FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Parameter,
                        commandParameterAst.Extent);
                return AstVisitAction.StopVisit;
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        ///  Checks to see if this variable expression is the symbol we are looking for.
        /// </summary>
        /// <param name="variableExpressionAst">A VariableExpressionAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            if (this.IsPositionInExtent(variableExpressionAst.Extent))
            {
                this.FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Variable,
                        variableExpressionAst.Extent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Is the position of the given location is in the ast's extent
        /// </summary>
        /// <param name="extent">The script extent of the element</param>
        /// <returns>True if the given position is in the range of the element's extent </returns>
        private bool IsPositionInExtent(IScriptExtent extent)
        {
            return (extent.StartLineNumber == lineNumber &&
                    extent.StartColumnNumber <= columnNumber &&
                    extent.EndColumnNumber >= columnNumber);
        }
    }
}
