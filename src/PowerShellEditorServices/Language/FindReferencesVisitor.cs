//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Language
{
    /// <summary>
    /// The visitor used to find the references of a symbol in a script's AST
    /// </summary>
    internal class FindReferencesVisitor : AstVisitor
    {
        private SymbolReference symbolRef;
        
        public List<SymbolReference> FoundReferences { get; set; }

        public FindReferencesVisitor(SymbolReference symbolRef)
        {
            this.symbolRef = symbolRef;
            this.FoundReferences = new List<SymbolReference>();
        }

        /// <summary>
        /// Decides if the current command is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Function 
        /// and have the same name as the symbol
        /// </summary>
        /// <param name="commandAst">A CommandAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            Ast commandNameAst = commandAst.CommandElements[0];
            if(symbolRef.SymbolType.Equals(SymbolType.Function) &&
                commandNameAst.Extent.Text.Equals(symbolRef.ScriptRegion.Text))
            {
                this.FoundReferences.Add(new SymbolReference(
                                        SymbolType.Function,
                                        commandNameAst.Extent));
            }
            return base.VisitCommand(commandAst);
        }

        /// <summary>
        /// Decides if the current function defintion is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Function and have the same name as the symbol
        /// </summary>
        /// <param name="functionDefinitionAst">A functionDefinitionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            int startColumnNumber =
                functionDefinitionAst.Extent.Text.IndexOf(
                    functionDefinitionAst.Name) + 1;

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = functionDefinitionAst.Name,
                StartLineNumber = functionDefinitionAst.Extent.StartLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = startColumnNumber + functionDefinitionAst.Name.Length
            };

            if (symbolRef.SymbolType.Equals(SymbolType.Function) &&
                nameExtent.Text.Equals(symbolRef.SymbolName))
            {
                this.FoundReferences.Add(new SymbolReference(
                                          SymbolType.Function,
                                          nameExtent));
            }
            return base.VisitFunctionDefinition(functionDefinitionAst);
        }

        /// <summary>
        /// Decides if the current function defintion is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Parameter and have the same name as the symbol 
        /// </summary>
        /// <param name="commandParameterAst">A commandParameterAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitCommandParameter(CommandParameterAst commandParameterAst)
        {
            if (symbolRef.SymbolType.Equals(SymbolType.Parameter) &&
                commandParameterAst.Extent.Text.Equals(symbolRef.SymbolName))
            {
                this.FoundReferences.Add(new SymbolReference(
                                         SymbolType.Parameter,
                                         commandParameterAst.Extent));
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Decides if the current function defintion is a reference of the symbol being searched for.
        /// A reference of the symbol will be a of type SymbolType.Variable and have the same name as the symbol  
        /// </summary>
        /// <param name="variableExpressionAst">A variableExpressionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            if(symbolRef.SymbolType.Equals(SymbolType.Variable) &&
                variableExpressionAst.Extent.Text.Equals(symbolRef.SymbolName))
            {
                this.FoundReferences.Add(new SymbolReference(
                                         SymbolType.Variable,
                                         variableExpressionAst.Extent));
            }
            return AstVisitAction.Continue;
        }
    }
}