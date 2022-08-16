// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        /// Adds each function definition as a
        /// </summary>
        /// <param name="functionDefinitionAst">A functionDefinitionAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            // Extent for constructors and method trigger both this and VisitFunctionMember(). Covered in the latter.
            // This will not exclude nested functions as they have ScriptBlockAst as parent
            if (functionDefinitionAst.Parent is FunctionMemberAst)
            {
                return AstVisitAction.Continue;
            }

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
        /// Checks to see if this variable expression is the symbol we are looking for.
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

        /// <summary>
        /// Adds class and AST to symbol reference list
        /// </summary>
        public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
        {
            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = typeDefinitionAst.Name,
                StartLineNumber = typeDefinitionAst.Extent.StartLineNumber,
                EndLineNumber = typeDefinitionAst.Extent.EndLineNumber,
                StartColumnNumber = typeDefinitionAst.Extent.StartColumnNumber,
                EndColumnNumber = typeDefinitionAst.Extent.EndColumnNumber,
                File = typeDefinitionAst.Extent.File
            };

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
        public override AstVisitAction VisitFunctionMember(FunctionMemberAst functionMemberAst)
        {
            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = GetMethodOverloadName(functionMemberAst),
                StartLineNumber = functionMemberAst.Extent.StartLineNumber,
                EndLineNumber = functionMemberAst.Extent.EndLineNumber,
                StartColumnNumber = functionMemberAst.Extent.StartColumnNumber,
                EndColumnNumber = functionMemberAst.Extent.EndColumnNumber,
                File = functionMemberAst.Extent.File
            };

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
        /// Gets the method or constructor name with parameters for current overload.
        /// </summary>
        /// <param name="functionMemberAst">A FunctionMemberAst object in the script's AST</param>
        /// <returns>Function member name with parameter types and names</returns>
        private static string GetMethodOverloadName(FunctionMemberAst functionMemberAst) {
            if (functionMemberAst.Parameters.Count > 0)
            {
                List<string> parameters = new(functionMemberAst.Parameters.Count);
                foreach (ParameterAst param in functionMemberAst.Parameters)
                {
                    parameters.Add(param.Extent.Text);
                }

                string paramString = string.Join(", ", parameters);
                return string.Concat(functionMemberAst.Name, "(", paramString, ")");
            }
            else
            {
                return string.Concat(functionMemberAst.Name, "()");
            }
        }

        /// <summary>
        /// Adds class property AST to symbol reference list
        /// </summary>
        public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = propertyMemberAst.Name,
                StartLineNumber = propertyMemberAst.Extent.StartLineNumber,
                EndLineNumber = propertyMemberAst.Extent.EndLineNumber,
                StartColumnNumber = propertyMemberAst.Extent.StartColumnNumber,
                EndColumnNumber = propertyMemberAst.Extent.EndColumnNumber,
                File = propertyMemberAst.Extent.File
            };

            SymbolReferences.Add(
                new SymbolReference(
                    SymbolType.Property,
                    nameExtent));

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Adds DSC configuration AST to symbol reference list
        /// </summary>
        public override AstVisitAction VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst)
        {
            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = configurationDefinitionAst.InstanceName.Extent.Text,
                StartLineNumber = configurationDefinitionAst.Extent.StartLineNumber,
                EndLineNumber = configurationDefinitionAst.Extent.EndLineNumber,
                StartColumnNumber = configurationDefinitionAst.Extent.StartColumnNumber,
                EndColumnNumber = configurationDefinitionAst.Extent.EndColumnNumber,
                File = configurationDefinitionAst.Extent.File
            };

            SymbolReferences.Add(
                new SymbolReference(
                    SymbolType.Configuration,
                    nameExtent));

            return AstVisitAction.Continue;
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
