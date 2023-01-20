// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using System.Collections.Generic;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services;

/// <summary>
/// Represents the symbols that are referenced and their locations within a single document.
/// </summary>
internal sealed class ReferenceTable
{
    private readonly ScriptFile _parent;

    private readonly ConcurrentDictionary<string, ConcurrentBag<SymbolReference>> _symbolReferences = new(StringComparer.OrdinalIgnoreCase);

    private bool _isInited;

    public ReferenceTable(ScriptFile parent) => _parent = parent;

    /// <summary>
    /// Clears the reference table causing it to re-scan the source AST when queried.
    /// </summary>
    public void TagAsChanged()
    {
        _symbolReferences.Clear();
        _isInited = false;
    }

    /// <summary>
    /// Prefer checking if the dictionary has contents to determine if initialized. The field
    /// `_isInited` is to guard against re-scanning files with no command references, but will
    /// generally be less reliable of a check.
    /// </summary>
    private bool IsInitialized => !_symbolReferences.IsEmpty || _isInited;

    internal bool TryGetReferences(string command, out ConcurrentBag<SymbolReference>? references)
    {
        EnsureInitialized();
        return _symbolReferences.TryGetValue(command, out references);
    }

    // TODO: Should this be improved, or pre-sorted?
    internal IReadOnlyList<SymbolReference> GetAllReferences()
    {
        EnsureInitialized();
        List<SymbolReference> allReferences = new();
        foreach (ConcurrentBag<SymbolReference> bag in _symbolReferences.Values)
        {
            allReferences.AddRange(bag);
        }
        return allReferences;
    }

    internal void EnsureInitialized()
    {
        if (IsInitialized)
        {
            return;
        }

        _parent.ScriptAst.Visit(new ReferenceVisitor(this));
    }

    private static bool ExtentIsEmpty(IScriptExtent e) => string.IsNullOrEmpty(e.File) &&
        e.StartLineNumber == 0 && e.StartColumnNumber == 0 &&
        e.EndLineNumber == 0 && e.EndColumnNumber == 0 &&
        string.IsNullOrEmpty(e.Text);

    private void AddReference(SymbolType type, string name, IScriptExtent nameExtent, IScriptExtent extent, bool isDeclaration = false)
    {
        // We have to exclude implicit things like `$this` that don't actually exist.
        if (ExtentIsEmpty(extent))
        {
            return;
        }

        SymbolReference symbol = new(type, name, nameExtent, extent, _parent, isDeclaration);
        _symbolReferences.AddOrUpdate(
            name,
            _ => new ConcurrentBag<SymbolReference> { symbol },
            (_, existing) =>
            {
                existing.Add(symbol);
                return existing;
            });
    }

    // TODO: Reconstruct this to take an action lambda that returns a visit action and accepts a
    // symbol. Then ReferenceTable can add a reference and find symbol can just stop.
    //
    // TODO: Have a symbol name and a separate display name, the first minimally the text so the
    // buckets work, the second usually a more complete signature for e.g. outline view.
    private sealed class ReferenceVisitor : AstVisitor2
    {
        private readonly ReferenceTable _references;

        public ReferenceVisitor(ReferenceTable references) => _references = references;

        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            string? commandName = VisitorUtils.GetCommandName(commandAst);
            if (string.IsNullOrEmpty(commandName))
            {
                return AstVisitAction.Continue;
            }

            _references.AddReference(
                SymbolType.Function,
                CommandHelpers.StripModuleQualification(commandName, out _),
                commandAst.CommandElements[0].Extent,
                commandAst.Extent);

            return AstVisitAction.Continue;
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
            _references.AddReference(
                symbolType,
                functionDefinitionAst.Name,
                nameExtent,
                functionDefinitionAst.Extent,
                isDeclaration: true);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitCommandParameter(CommandParameterAst commandParameterAst)
        {
            _references.AddReference(
                SymbolType.Parameter,
                commandParameterAst.Extent.Text,
                commandParameterAst.Extent,
                commandParameterAst.Extent,
                isDeclaration: true);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            // TODO: Consider tracking unscoped variable references only when they declared within
            // the same function definition.
            _references.AddReference(
                SymbolType.Variable,
                $"${variableExpressionAst.VariablePath.UserPath}",
                variableExpressionAst.Extent,
                variableExpressionAst.Extent, // TODO: Maybe parent?
                isDeclaration: variableExpressionAst.Parent is AssignmentStatementAst or ParameterAst);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
        {
            SymbolType symbolType = typeDefinitionAst.IsEnum
                ? SymbolType.Enum
                : SymbolType.Class;

            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(typeDefinitionAst);
            _references.AddReference(
                symbolType,
                typeDefinitionAst.Name,
                nameExtent,
                typeDefinitionAst.Extent,
                isDeclaration: true);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitTypeExpression(TypeExpressionAst typeExpressionAst)
        {
            _references.AddReference(
                SymbolType.Type,
                typeExpressionAst.TypeName.Name,
                typeExpressionAst.Extent,
                typeExpressionAst.Extent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitTypeConstraint(TypeConstraintAst typeConstraintAst)
        {
            _references.AddReference(
                SymbolType.Type,
                typeConstraintAst.TypeName.Name,
                typeConstraintAst.Extent,
                typeConstraintAst.Extent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitFunctionMember(FunctionMemberAst functionMemberAst)
        {
            SymbolType symbolType = functionMemberAst.IsConstructor
                ? SymbolType.Constructor
                : SymbolType.Method;

            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(
                functionMemberAst,
                useQualifiedName: false,
                includeReturnType: false);

            _references.AddReference(
                symbolType,
                functionMemberAst.Name, // We bucket all the overloads.
                nameExtent,
                functionMemberAst.Extent,
                isDeclaration: true);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            SymbolType symbolType =
                propertyMemberAst.Parent is TypeDefinitionAst typeAst && typeAst.IsEnum
                    ? SymbolType.EnumMember : SymbolType.Property;

            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(propertyMemberAst, false, false);
            _references.AddReference(
                symbolType,
                nameExtent.Text,
                nameExtent,
                propertyMemberAst.Extent,
                isDeclaration: true);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitMemberExpression(MemberExpressionAst memberExpressionAst)
        {
            string? memberName = memberExpressionAst.Member is StringConstantExpressionAst stringConstant ? stringConstant.Value : null;
            if (string.IsNullOrEmpty(memberName))
            {
                return AstVisitAction.Continue;
            }

            _references.AddReference(
                SymbolType.Property,
                memberName,
                memberExpressionAst.Member.Extent,
                memberExpressionAst.Extent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitInvokeMemberExpression(InvokeMemberExpressionAst methodCallAst)
        {
            string? memberName = methodCallAst.Member is StringConstantExpressionAst stringConstant ? stringConstant.Value : null;
            if (string.IsNullOrEmpty(memberName))
            {
                return AstVisitAction.Continue;
            }

            _references.AddReference(
                SymbolType.Method,
                memberName,
                methodCallAst.Member.Extent,
                methodCallAst.Extent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitConfigurationDefinition(ConfigurationDefinitionAst configurationDefinitionAst)
        {
            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(configurationDefinitionAst);
            _references.AddReference(
                SymbolType.Configuration,
                nameExtent.Text,
                nameExtent,
                configurationDefinitionAst.Extent,
                isDeclaration: true);

            return AstVisitAction.Continue;
        }
    }
}
