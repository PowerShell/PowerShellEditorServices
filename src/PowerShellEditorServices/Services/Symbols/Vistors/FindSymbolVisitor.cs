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
        private readonly bool returnMemberSignature;

        public SymbolReference FoundSymbolReference { get; private set; }

        public FindSymbolVisitor(
            int lineNumber,
            int columnNumber,
            bool includeDefinitions,
            bool returnMemberSignature)
        {
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
            this.includeDefinitions = includeDefinitions;
            this.returnMemberSignature = returnMemberSignature;
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

            IScriptExtent nameExtent;

            if (includeDefinitions)
            {
                nameExtent = new ScriptExtent()
                {
                    Text = functionDefinitionAst.Name,
                    StartLineNumber = functionDefinitionAst.Extent.StartLineNumber,
                    EndLineNumber = functionDefinitionAst.Extent.EndLineNumber,
                    StartColumnNumber = functionDefinitionAst.Extent.StartColumnNumber,
                    EndColumnNumber = functionDefinitionAst.Extent.EndColumnNumber,
                    File = functionDefinitionAst.Extent.File
                };
            }
            else
            {
                // We only want the function name
                nameExtent = VisitorUtils.GetNameExtent(functionDefinitionAst);
            }

            if (nameExtent.Contains(lineNumber, columnNumber))
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
        /// Is the position of the given location is in the ast's extent.
        /// Only works with single-line extents like name extents.
        /// Use <see cref="ObjectExtensions.Contains"/> extension for definition extents.
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
            // We only want the method/ctor name. Get start-location for name
            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionMemberAst, returnMemberSignature);

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
            IScriptExtent nameExtent;

            if (includeDefinitions)
            {
                nameExtent = new ScriptExtent()
                {
                    Text = typeDefinitionAst.Name,
                    StartLineNumber = typeDefinitionAst.Extent.StartLineNumber,
                    EndLineNumber = typeDefinitionAst.Extent.EndLineNumber,
                    StartColumnNumber = typeDefinitionAst.Extent.StartColumnNumber,
                    EndColumnNumber = typeDefinitionAst.Extent.EndColumnNumber,
                    File = typeDefinitionAst.Extent.File
                };
            }
            else
            {
                // We only want the type name
                nameExtent = VisitorUtils.GetNameExtent(typeDefinitionAst);
            }

            if (nameExtent.Contains(lineNumber, columnNumber))
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
            // Show only type name (skip leading '['). Offset by StartColumn to include indentation etc.
            int startColumnNumber = typeExpressionAst.Extent.StartColumnNumber + 1;

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
            // Show only type name (skip leading '[' if present). It's not present for inherited types
            // Offset by StartColumn to include indentation etc.
            int startColumnNumber =
                typeConstraintAst.Extent.Text[0] == '[' ?
                    typeConstraintAst.Extent.StartColumnNumber + 1 : typeConstraintAst.Extent.StartColumnNumber;

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
            // We only want the configuration name. Get start-location for name
            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(configurationDefinitionAst);

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
        /// Checks to see if this property member is the symbol we are looking for.
        /// </summary>
        /// <param name="propertyMemberAst">A PropertyMemberAst object in the script's AST</param>
        /// <returns>A decision to stop searching if the right symbol was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            // We only want the property name. Get start-location for name
            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(propertyMemberAst, returnMemberSignature);

            if (IsPositionInExtent(nameExtent))
            {
                SymbolType symbolType =
                    propertyMemberAst.Parent is TypeDefinitionAst typeAst && typeAst.IsEnum ?
                        SymbolType.EnumMember : SymbolType.Property;

                FoundSymbolReference =
                    new SymbolReference(
                        symbolType,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
        }
    }
}
