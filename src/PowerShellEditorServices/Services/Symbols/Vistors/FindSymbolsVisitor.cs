// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.PowerShell.EditorServices.Utility;
using System.Collections.Generic;
using System.Management.Automation.Language;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The visitor used to find all the symbols (variables, functions and class defs etc) in the AST.
    /// </summary>
    internal class FindSymbolsVisitor : AstVisitor2
    {
        public List<SymbolReference> SymbolReferences { get; }

        public FindSymbolsVisitor() => SymbolReferences = new List<SymbolReference>();

        /// <summary>
        /// Adds each function definition to symbol reference list
        /// </summary>
        /// <param name="functionDefinitionAst">A FunctionDefinitionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            // Extent for constructors and method trigger both this and VisitFunctionMember(). Covered in the latter.
            // This will not exclude nested functions as they have ScriptBlockAst as parent
            if (functionDefinitionAst.Parent is FunctionMemberAst)
            {
                return AstVisitAction.Continue;
            }

            (int startColumn, int startLine) = VisitorUtils.GetNameStartColumnAndLineFromAst(functionDefinitionAst);
            IScriptExtent nameExtent = GetNewExtent(functionDefinitionAst, functionDefinitionAst.Name, startLine, startColumn);

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
        /// Adds each script scoped variable assignment to symbol reference list
        /// </summary>
        /// <param name="variableExpressionAst">A VariableExpressionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
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

        /// <summary>
        /// Adds class and enum AST to symbol reference list
        /// </summary>
        /// <param name="typeDefinitionAst">A TypeDefinitionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
        {
            (int startColumn, int startLine) = VisitorUtils.GetNameStartColumnAndLineFromAst(typeDefinitionAst);
            IScriptExtent nameExtent = GetNewExtent(typeDefinitionAst, typeDefinitionAst.Name, startLine, startColumn);

            SymbolType symbolType =
                typeDefinitionAst.IsEnum ?
                    SymbolType.Enum : SymbolType.Class;

            SymbolReferences.Add(
                new SymbolReference(
                    symbolType,
                    nameExtent));

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Adds class method and constructor AST to symbol reference list
        /// </summary>
        /// <param name="functionMemberAst">A FunctionMemberAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitFunctionMember(FunctionMemberAst functionMemberAst)
        {
            (int startColumn, int startLine) = VisitorUtils.GetNameStartColumnAndLineFromAst(functionMemberAst);
            IScriptExtent nameExtent = GetNewExtent(functionMemberAst, VisitorUtils.GetMemberOverloadName(functionMemberAst), startLine, startColumn);

            SymbolType symbolType =
                functionMemberAst.IsConstructor ?
                    SymbolType.Constructor : SymbolType.Method;

            SymbolReferences.Add(
                new SymbolReference(
                    symbolType,
                    nameExtent));

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Adds class property AST to symbol reference list
        /// </summary>
        /// <param name="propertyMemberAst">A PropertyMemberAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            SymbolType symbolType =
                propertyMemberAst.Parent is TypeDefinitionAst typeAst && typeAst.IsEnum ?
                    SymbolType.EnumMember : SymbolType.Property;

            bool isEnumMember = symbolType.Equals(SymbolType.EnumMember);
            (int startColumn, int startLine) = VisitorUtils.GetNameStartColumnAndLineFromAst(propertyMemberAst, isEnumMember);
            IScriptExtent nameExtent = GetNewExtent(propertyMemberAst, propertyMemberAst.Name, startLine, startColumn);

            SymbolReferences.Add(
                new SymbolReference(
                    symbolType,
                    nameExtent));

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Adds DSC configuration AST to symbol reference list
        /// </summary>
        /// <param name="configurationDefinitionAst">A ConfigurationDefinitionAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
        public override AstVisitAction VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst)
        {
            (int startColumn, int startLine) = VisitorUtils.GetNameStartColumnAndLineFromAst(configurationDefinitionAst);
            IScriptExtent nameExtent = GetNewExtent(configurationDefinitionAst, configurationDefinitionAst.InstanceName.Extent.Text, startLine, startColumn);

            SymbolReferences.Add(
                new SymbolReference(
                    SymbolType.Configuration,
                    nameExtent));

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Gets a new ScriptExtent for a given Ast with same range but modified Text
        /// </summary>
        private static ScriptExtent GetNewExtent(Ast ast, string text, int startLine, int startColumn)
        {
            return new ScriptExtent()
            {
                Text = text,
                StartLineNumber = startLine,
                EndLineNumber = ast.Extent.EndLineNumber,
                StartColumnNumber = startColumn,
                EndColumnNumber = ast.Extent.EndColumnNumber,
                File = ast.Extent.File
            };
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
        /// <param name="hashtableAst">A HashtableAst in the script's AST</param>
        /// <returns>A visit action that continues the search for references</returns>
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
