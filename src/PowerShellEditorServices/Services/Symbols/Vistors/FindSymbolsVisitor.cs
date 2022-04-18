﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The visitor used to find all the symbols (function and class defs) in the AST.
    /// </summary>
    /// <remarks>
    /// Requires PowerShell v3 or higher
    /// </remarks>
    internal class FindSymbolsVisitor : AstVisitor
    {
        public List<SymbolReference> SymbolReferences { get; }

        public FindSymbolsVisitor() => SymbolReferences = new List<SymbolReference>();

        /// <summary>
        /// Adds each function definition as a
        /// </summary>
        /// <param name="functionDefinitionAst">A functionDefinitionAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = functionDefinitionAst.Name,
                StartLineNumber = functionDefinitionAst.Extent.StartLineNumber,
                EndLineNumber = functionDefinitionAst.Extent.EndLineNumber,
                StartColumnNumber = functionDefinitionAst.Extent.StartColumnNumber,
                EndColumnNumber = functionDefinitionAst.Extent.EndColumnNumber,
                File = functionDefinitionAst.Extent.File
            };

            SymbolType symbolType =
                functionDefinitionAst.IsWorkflow ?
                    SymbolType.Workflow : SymbolType.Function;

            SymbolReferences.Add(
                new SymbolReference(
                    symbolType,
                    nameExtent));

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
            if (!IsAssignedAtScriptScope(variableExpressionAst))
            {
                return AstVisitAction.Continue;
            }

            SymbolReferences.Add(
                new SymbolReference(
                    SymbolType.Variable,
                    variableExpressionAst.Extent));

            return AstVisitAction.Continue;
        }

        private static bool IsAssignedAtScriptScope(VariableExpressionAst variableExpressionAst)
        {
            Ast parent = variableExpressionAst.Parent;
            if (parent is not AssignmentStatementAst)
            {
                return false;
            }

            parent = parent.Parent;
            return parent is null || parent.Parent is null || parent.Parent.Parent is null;
        }
    }

    /// <summary>
    /// Visitor to find all the keys in Hashtable AST
    /// </summary>
    internal class FindHashtableSymbolsVisitor : AstVisitor
    {
        /// <summary>
        /// List of symbols (keys) found in the hashtable
        /// </summary>
        public List<SymbolReference> SymbolReferences { get; }

        /// <summary>
        /// Initializes a new instance of FindHashtableSymbolsVisitor class
        /// </summary>
        public FindHashtableSymbolsVisitor() => SymbolReferences = new List<SymbolReference>();

        /// <summary>
        /// Adds keys in the input hashtable to the symbol reference
        /// </summary>
        public override AstVisitAction VisitHashtable(HashtableAst hashtableAst)
        {
            if (hashtableAst.KeyValuePairs == null)
            {
                return AstVisitAction.Continue;
            }

            foreach (System.Tuple<ExpressionAst, StatementAst> kvp in hashtableAst.KeyValuePairs)
            {
                if (kvp.Item1 is StringConstantExpressionAst keyStrConstExprAst)
                {
                    IScriptExtent nameExtent = new ScriptExtent()
                    {
                        Text = keyStrConstExprAst.Value,
                        StartLineNumber = kvp.Item1.Extent.StartLineNumber,
                        EndLineNumber = kvp.Item2.Extent.EndLineNumber,
                        StartColumnNumber = kvp.Item1.Extent.StartColumnNumber,
                        EndColumnNumber = kvp.Item2.Extent.EndColumnNumber,
                        File = hashtableAst.Extent.File
                    };

                    const SymbolType symbolType = SymbolType.HashtableKey;

                    SymbolReferences.Add(
                        new SymbolReference(
                            symbolType,
                            nameExtent));
                }
            }

            return AstVisitAction.Continue;
        }
    }
}
