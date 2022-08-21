// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols
{
    /// <summary>
    /// The visitor used to find the definition of a symbol
    /// </summary>
    internal class FindDeclarationVisitor : AstVisitor2
    {
        private readonly SymbolReference symbolRef;
        private readonly string variableName;

        public SymbolReference FoundDeclaration { get; private set; }

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
            // Extent for constructors and method trigger both this and VisitFunctionMember(). Covered in the latter.
            // This will not exclude nested functions as they have ScriptBlockAst as parent
            if (functionDefinitionAst.Parent is FunctionMemberAst)
            {
                return AstVisitAction.Continue;
            }

            // We compare to the SymbolName instead of its text because it may have been resolved
            // from an alias.
            if (symbolRef.SymbolType.Equals(SymbolType.Function) &&
                functionDefinitionAst.Name.Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                // Get the start column number of the function name,
                // instead of the the start column of 'function' and create new extent for the functionName
                IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionDefinitionAst);

                FoundDeclaration =
                    new SymbolReference(
                        SymbolType.Function,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return base.VisitFunctionDefinition(functionDefinitionAst);
        }

        /// <summary>
        /// Decides if the current type definition is the right definition
        /// for the symbol being searched for. The definition of the symbol will be a of type
        /// SymbolType.Enum or SymbolType.Class and have the same name as the symbol
        /// </summary>
        /// <param name="typeDefinitionAst">A TypeDefinitionAst in the script's AST</param>
        /// <returns>A decision to stop searching if the right TypeDefinitionAst was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
        {
            SymbolType symbolType =
                typeDefinitionAst.IsEnum ?
                    SymbolType.Enum : SymbolType.Class;

            if ((symbolRef.SymbolType is SymbolType.Type || symbolRef.SymbolType.Equals(symbolType)) &&
                typeDefinitionAst.Name.Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                // We only want the type name. Get start-location for name
                IScriptExtent nameExtent = VisitorUtils.GetNameExtent(typeDefinitionAst);

                FoundDeclaration =
                    new SymbolReference(
                        symbolType,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Decides if the current function member is the right definition
        /// for the symbol being searched for. The definition of the symbol will be a of type
        /// SymbolType.Constructor or SymbolType.Method and have the same name as the symbol
        /// </summary>
        /// <param name="functionMemberAst">A FunctionMemberAst in the script's AST</param>
        /// <returns>A decision to stop searching if the right FunctionMemberAst was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitFunctionMember(FunctionMemberAst functionMemberAst)
        {
            SymbolType symbolType =
                functionMemberAst.IsConstructor ?
                    SymbolType.Constructor : SymbolType.Method;

            if (symbolRef.SymbolType.Equals(symbolType) &&
                VisitorUtils.GetMemberOverloadName(functionMemberAst, true, false).Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                // We only want the method/ctor name. Get start-location for name
                IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionMemberAst, true, false);

                FoundDeclaration =
                    new SymbolReference(
                        symbolType,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
        }

        /// <summary>
        /// Decides if the current property member is the right definition
        /// for the symbol being searched for. The definition of the symbol will be a of type
        /// SymbolType.Property or SymbolType.EnumMember and have the same name as the symbol
        /// </summary>
        /// <param name="propertyMemberAst">A PropertyMemberAst in the script's AST</param>
        /// <returns>A decision to stop searching if the right PropertyMemberAst was found,
        /// or a decision to continue if it wasn't found</returns>
        public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            SymbolType symbolType =
                propertyMemberAst.Parent is TypeDefinitionAst typeAst && typeAst.IsEnum ?
                    SymbolType.EnumMember : SymbolType.Property;

            if (symbolRef.SymbolType.Equals(symbolType) &&
                VisitorUtils.GetMemberOverloadName(propertyMemberAst, false).Equals(symbolRef.SymbolName, StringComparison.CurrentCultureIgnoreCase))
            {
                // We only want the property name. Get start-location for name
                IScriptExtent nameExtent = VisitorUtils.GetNameExtent(propertyMemberAst, false);

                FoundDeclaration =
                    new SymbolReference(
                        SymbolType.Property,
                        nameExtent);

                return AstVisitAction.StopVisit;
            }

            return AstVisitAction.Continue;
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
            FindDeclarationVariableExpressionVisitor visitor = new(symbolRef);
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
            private readonly SymbolReference symbolRef;
            private readonly string variableName;

            public SymbolReference FoundDeclaration { get; private set; }

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

            public override AstVisitAction VisitMemberExpression(MemberExpressionAst functionDefinitionAst) =>
                // We don't want to discover any variables in member expressisons (`$something.Foo`)
                AstVisitAction.SkipChildren;

            public override AstVisitAction VisitIndexExpression(IndexExpressionAst functionDefinitionAst) =>
                // We don't want to discover any variables in index expressions (`$something[0]`)
                AstVisitAction.SkipChildren;
        }
    }
}
