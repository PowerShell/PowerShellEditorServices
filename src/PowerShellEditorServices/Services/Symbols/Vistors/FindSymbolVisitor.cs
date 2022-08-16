// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The visitor used to find the symbol at a specific location in the AST
    /// </summary>
    internal class FindSymbolVisitor : AstVisitor2
    {
        private readonly int lineNumber;
        private readonly int columnNumber;
        private readonly bool includeDefinitions;

        public SymbolReference FoundSymbolReference { get; private set; }

        public FindSymbolVisitor(
            int lineNumber,
            int columnNumber,
            bool includeDefinitions)
        {
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
            this.includeDefinitions = includeDefinitions;
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

            if (IsPositionInExtent(commandNameAst.Extent))
            {
                FoundSymbolReference =
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
            // Extent for constructors and method trigger both this and VisitFunctionMember(). Covered in the latter.
            // This will not exclude nested functions as they have ScriptBlockAst as parent
            if (functionDefinitionAst.Parent is FunctionMemberAst)
            {
                return AstVisitAction.Continue;
            }

            int startLineNumber = functionDefinitionAst.Extent.StartLineNumber;
            int startColumnNumber = functionDefinitionAst.Extent.StartColumnNumber;
            int endLineNumber = functionDefinitionAst.Extent.EndLineNumber;
            int endColumnNumber = functionDefinitionAst.Extent.EndColumnNumber;

            if (!includeDefinitions)
            {
                // We only want the function name
                (int startColumn, int startLine) = VisitorUtils.GetNameStartColumnAndLineNumbersFromAst(functionDefinitionAst);
                startLineNumber = startLine;
                startColumnNumber = startColumn;
                endLineNumber = startLine;
                endColumnNumber = startColumn + functionDefinitionAst.Name.Length;
            }

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = functionDefinitionAst.Name,
                StartLineNumber = startLineNumber,
                EndLineNumber = endLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = endColumnNumber,
                File = functionDefinitionAst.Extent.File
            };

            if (IsPositionInExtent(nameExtent))
            {
                FoundSymbolReference =
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
            if (IsPositionInExtent(commandParameterAst.Extent))
            {
                FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Parameter,
                        commandParameterAst.Extent);
                return AstVisitAction.StopVisit;
            }
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
            if (IsPositionInExtent(variableExpressionAst.Extent))
            {
                FoundSymbolReference =
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
            return extent.StartLineNumber == lineNumber &&
                    extent.StartColumnNumber <= columnNumber &&
                    extent.EndColumnNumber >= columnNumber;
        }

        /// <summary>
        /// Checks to see if this function member is the symbol we are looking for.
        /// </summary>
        /// <param name="functionMemberAst">A FunctionMemberAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitFunctionMember(FunctionMemberAst functionMemberAst)
        {
            // Show only method/ctor name. Offset by StartColumn to include indentation etc.
            int startColumnNumber =
                functionMemberAst.Extent.StartColumnNumber +
                functionMemberAst.Extent.Text.IndexOf(functionMemberAst.Name);

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = functionMemberAst.Name,
                StartLineNumber = functionMemberAst.Extent.StartLineNumber,
                EndLineNumber = functionMemberAst.Extent.StartLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = startColumnNumber + functionMemberAst.Name.Length,
                File = functionMemberAst.Extent.File
            };

            if (IsPositionInExtent(nameExtent))
            {
                SymbolType symbolType =
                    functionMemberAst.IsConstructor ?
                        SymbolType.Constructor : SymbolType.Method;

                FoundSymbolReference =
                    new SymbolReference(
                        symbolType,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Checks to see if this type definition is the symbol we are looking for.
        /// </summary>
        /// <param name="typeDefinitionAst">A TypeDefinitionAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
        {
            int startLineNumber = typeDefinitionAst.Extent.StartLineNumber;
            int startColumnNumber = typeDefinitionAst.Extent.StartColumnNumber;
            int endLineNumber = typeDefinitionAst.Extent.EndLineNumber;
            int endColumnNumber = typeDefinitionAst.Extent.EndColumnNumber;

            if (!includeDefinitions)
            {
                // We only want the function name
                startColumnNumber =
                    typeDefinitionAst.Extent.StartColumnNumber +
                    typeDefinitionAst.Extent.Text.IndexOf(typeDefinitionAst.Name);
                startLineNumber = typeDefinitionAst.Extent.StartLineNumber;
                endColumnNumber = startColumnNumber + typeDefinitionAst.Name.Length;
                endLineNumber = typeDefinitionAst.Extent.StartLineNumber;
            }

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = typeDefinitionAst.Name,
                StartLineNumber = startLineNumber,
                EndLineNumber = endLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = endColumnNumber,
                File = typeDefinitionAst.Extent.File
            };

            if (IsPositionInExtent(nameExtent))
            {
                SymbolType symbolType =
                    typeDefinitionAst.IsEnum ?
                        SymbolType.Enum : SymbolType.Class;

                FoundSymbolReference =
                    new SymbolReference(
                        symbolType,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Checks to see if this type expression is the symbol we are looking for.
        /// </summary>
        /// <param name="typeExpressionAst">A TypeExpressionAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitTypeExpression(TypeExpressionAst typeExpressionAst)
        {
            // Show only type name. Offset by StartColumn to include indentation etc.
            int startColumnNumber =
                typeExpressionAst.Extent.StartColumnNumber +
                typeExpressionAst.Extent.Text.IndexOf(typeExpressionAst.TypeName.Name);

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = typeExpressionAst.TypeName.Name,
                StartLineNumber = typeExpressionAst.Extent.StartLineNumber,
                EndLineNumber = typeExpressionAst.Extent.StartLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = startColumnNumber + typeExpressionAst.TypeName.Name.Length,
                File = typeExpressionAst.Extent.File
            };

            if (IsPositionInExtent(nameExtent))
            {
                FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Type,
                        nameExtent);
                return AstVisitAction.StopVisit;
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Checks to see if this type constraint is the symbol we are looking for.
        /// </summary>
        /// <param name="typeConstraintAst">A TypeConstraintAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitTypeConstraint(TypeConstraintAst typeConstraintAst)
        {
            // Show only type name. Offset by StartColumn to include indentation etc.
            int startColumnNumber =
                typeConstraintAst.Extent.StartColumnNumber +
                typeConstraintAst.Extent.Text.IndexOf(typeConstraintAst.TypeName.Name);

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = typeConstraintAst.TypeName.Name,
                StartLineNumber = typeConstraintAst.Extent.StartLineNumber,
                EndLineNumber = typeConstraintAst.Extent.StartLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = startColumnNumber + typeConstraintAst.TypeName.Name.Length,
                File = typeConstraintAst.Extent.File
            };

            if (IsPositionInExtent(nameExtent))
            {
                FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Type,
                        nameExtent);
                return AstVisitAction.StopVisit;
            }
            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Checks to see if this configuration definition is the symbol we are looking for.
        /// </summary>
        /// <param name="configurationDefinitionAst">A ConfigurationDefinitionAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst)
        {
            string configurationName = configurationDefinitionAst.InstanceName.Extent.Text;

            // Show only configuration name. Offset by StartColumn to include indentation etc.
            int startColumnNumber =
                configurationDefinitionAst.Extent.StartColumnNumber +
                configurationDefinitionAst.Extent.Text.IndexOf(configurationName);

            IScriptExtent nameExtent = new ScriptExtent()
            {
                Text = configurationName,
                StartLineNumber = configurationDefinitionAst.Extent.StartLineNumber,
                EndLineNumber = configurationDefinitionAst.Extent.StartLineNumber,
                StartColumnNumber = startColumnNumber,
                EndColumnNumber = startColumnNumber + configurationName.Length,
                File = configurationDefinitionAst.Extent.File
            };

            if (IsPositionInExtent(nameExtent))
            {
                FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Configuration,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Checks to see if this variable expression is the symbol we are looking for.
        /// </summary>
        /// <param name="propertyMemberAst">A VariableExpressionAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            if (IsPositionInExtent(propertyMemberAst.Extent))
            {
                FoundSymbolReference =
                    new SymbolReference(
                        SymbolType.Property,
                        propertyMemberAst.Extent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
        }
    }
}
