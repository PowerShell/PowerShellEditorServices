// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.Symbols;

/// <summary>
/// The goal of this is to be our one and only visitor, which parses a file when necessary
/// performing Action, which takes a SymbolReference (that this visitor creates) and returns an
/// AstVisitAction. In this way, all our symbols are created with the same initialization logic.
/// </summary>
internal sealed class SymbolVisitor : AstVisitor2
{
    private readonly ScriptFile _file;

    private readonly Func<SymbolReference, AstVisitAction> _action;

    public SymbolVisitor(ScriptFile file, Func<SymbolReference, AstVisitAction> action)
    {
        _file = file;
        _action = action;
    }

    // TODO: Make all the display strings better (and performant).
    public override AstVisitAction VisitCommand(CommandAst commandAst)
    {
        string? commandName = VisitorUtils.GetCommandName(commandAst);
        if (commandName is null)
        {
            return AstVisitAction.Continue;
        }

        return _action(new SymbolReference(
            SymbolType.Function,
            CommandHelpers.StripModuleQualification(commandName, out _),
            commandName,
            commandAst.CommandElements[0].Extent,
            commandAst.Extent,
            _file,
            isDeclaration: false));
    }

    public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
    {
        SymbolType symbolType = functionDefinitionAst.IsWorkflow
            ? SymbolType.Workflow
            : SymbolType.Function;

        // Extent for constructors and method trigger both this and VisitFunctionMember(). Covered in the latter.
        // This will not exclude nested functions as they have ScriptBlockAst as parent
        if (functionDefinitionAst.Parent is FunctionMemberAst)
        {
            return AstVisitAction.Continue;
        }

        IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionDefinitionAst);
        return _action(new SymbolReference(
            symbolType,
            functionDefinitionAst.Name,
            VisitorUtils.GetFunctionDisplayName(functionDefinitionAst),
            nameExtent,
            functionDefinitionAst.Extent,
            _file,
            isDeclaration: true));
    }

    public override AstVisitAction VisitParameter(ParameterAst parameterAst)
    {
        // TODO: Can we fix the display name's type by visiting this in VisitVariableExpression and
        // getting the TypeConstraintAst somehow?
        return _action(new SymbolReference(
            SymbolType.Parameter,
            "$" + parameterAst.Name.VariablePath.UserPath,
            VisitorUtils.GetParamDisplayName(parameterAst),
            parameterAst.Name.Extent,
            parameterAst.Extent,
            _file,
            isDeclaration: true));
    }

    public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
    {
        // Parameters are visited earlier, and we don't want to skip their children because we do
        // want to visit their type constraints.
        if (variableExpressionAst.Parent is ParameterAst)
        {
            return AstVisitAction.Continue;
        }

        // TODO: Consider tracking unscoped variable references only when they declared within
        // the same function definition.
        return _action(new SymbolReference(
            SymbolType.Variable,
            "$" + variableExpressionAst.VariablePath.UserPath,
            "$" + variableExpressionAst.VariablePath.UserPath,
            variableExpressionAst.Extent,
            variableExpressionAst.Extent, // TODO: Maybe parent?
            _file,
            isDeclaration: variableExpressionAst.Parent is AssignmentStatementAst or ParameterAst));
    }

    public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
    {
        SymbolType symbolType = typeDefinitionAst.IsEnum
            ? SymbolType.Enum
            : SymbolType.Class;

        IScriptExtent nameExtent = VisitorUtils.GetNameExtent(typeDefinitionAst);
        return _action(new SymbolReference(
            symbolType,
            typeDefinitionAst.Name,
            (symbolType is SymbolType.Enum ? "enum " : "class ") + typeDefinitionAst.Name + " { }",
            nameExtent,
            typeDefinitionAst.Extent,
            _file,
            isDeclaration: true));
    }

    public override AstVisitAction VisitTypeExpression(TypeExpressionAst typeExpressionAst)
    {
        return _action(new SymbolReference(
            SymbolType.Type,
            typeExpressionAst.TypeName.Name,
            "(type) " + typeExpressionAst.TypeName.Name,
            typeExpressionAst.Extent,
            typeExpressionAst.Extent,
            _file,
            isDeclaration: false));
    }

    public override AstVisitAction VisitTypeConstraint(TypeConstraintAst typeConstraintAst)
    {
        return _action(new SymbolReference(
            SymbolType.Type,
            typeConstraintAst.TypeName.Name,
            "(type) " + typeConstraintAst.TypeName.Name,
            typeConstraintAst.Extent,
            typeConstraintAst.Extent,
            _file,
            isDeclaration: false));
    }

    public override AstVisitAction VisitFunctionMember(FunctionMemberAst functionMemberAst)
    {
        SymbolType symbolType = functionMemberAst.IsConstructor
            ? SymbolType.Constructor
            : SymbolType.Method;

        IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionMemberAst);

        return _action(new SymbolReference(
            symbolType,
            functionMemberAst.Name, // We bucket all the overloads.
            VisitorUtils.GetMemberOverloadName(functionMemberAst),
            nameExtent,
            functionMemberAst.Extent,
            _file,
            isDeclaration: true));
    }

    public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
    {
        // Enum members and properties are the "same" according to PowerShell, so the symbol name's
        // must be the same since we can't distinguish them in VisitMemberExpression.
        SymbolType symbolType =
            propertyMemberAst.Parent is TypeDefinitionAst typeAst && typeAst.IsEnum
                ? SymbolType.EnumMember
                : SymbolType.Property;

        return _action(new SymbolReference(
            symbolType,
            "$" + propertyMemberAst.Name,
            VisitorUtils.GetMemberOverloadName(propertyMemberAst),
            VisitorUtils.GetNameExtent(propertyMemberAst),
            propertyMemberAst.Extent,
            _file,
            isDeclaration: true));
    }

    public override AstVisitAction VisitMemberExpression(MemberExpressionAst memberExpressionAst)
    {
        string? memberName = memberExpressionAst.Member is StringConstantExpressionAst stringConstant ? stringConstant.Value : null;
        if (string.IsNullOrEmpty(memberName))
        {
            return AstVisitAction.Continue;
        }

        // TODO: It's too bad we can't get the property's real symbol and reuse its display string.
        return _action(new SymbolReference(
            SymbolType.Property,
#pragma warning disable CS8604 // Possible null reference argument.
            "$" + memberName,
#pragma warning restore CS8604
            "(property) " + memberName,
            memberExpressionAst.Member.Extent,
            memberExpressionAst.Extent,
            _file,
            isDeclaration: false));
    }

    public override AstVisitAction VisitInvokeMemberExpression(InvokeMemberExpressionAst methodCallAst)
    {
        string? memberName = methodCallAst.Member is StringConstantExpressionAst stringConstant ? stringConstant.Value : null;
        if (string.IsNullOrEmpty(memberName))
        {
            return AstVisitAction.Continue;
        }

        // TODO: It's too bad we can't get the member's real symbol and reuse its display string.
        return _action(new SymbolReference(
            SymbolType.Method,
#pragma warning disable CS8604 // Possible null reference argument.
            memberName,
#pragma warning restore CS8604
            "(method) " + memberName,
            methodCallAst.Member.Extent,
            methodCallAst.Extent,
            _file,
            isDeclaration: false));
    }

    public override AstVisitAction VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst)
    {
        string? name = configurationDefinitionAst.InstanceName is StringConstantExpressionAst stringConstant
            ? stringConstant.Value : null;
        if (string.IsNullOrEmpty(name))
        {
            return AstVisitAction.Continue;
        }

        IScriptExtent nameExtent = VisitorUtils.GetNameExtent(configurationDefinitionAst);
        return _action(new SymbolReference(
            SymbolType.Configuration,
#pragma warning disable CS8604 // Possible null reference argument.
            name,
#pragma warning restore CS8604
            "configuration " + name + " { }",
            nameExtent,
            configurationDefinitionAst.Extent,
            _file,
            isDeclaration: true));
    }
}
