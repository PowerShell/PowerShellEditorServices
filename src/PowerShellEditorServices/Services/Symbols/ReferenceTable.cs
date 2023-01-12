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

    private void AddReference(SymbolType type, string name, IScriptExtent extent)
    {
        SymbolReference symbol = new(type, name, extent, _parent);
        _symbolReferences.AddOrUpdate(
            name,
            _ => new ConcurrentBag<SymbolReference> { symbol },
            (_, existing) =>
            {
                existing.Add(symbol);
                return existing;
            });
    }

    // TODO: Should we move this to AstOperations.cs? It is highly coupled to `ReferenceTable`,
    // perhaps it doesn't have to be.
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
                commandAst.CommandElements[0].Extent
            );

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitFunctionDefinition(FunctionDefinitionAst functionDefinitionAst)
        {
            // Extent for constructors and method trigger both this and VisitFunctionMember(). Covered in the latter.
            // This will not exclude nested functions as they have ScriptBlockAst as parent
            if (functionDefinitionAst.Parent is FunctionMemberAst)
            {
                return AstVisitAction.Continue;
            }

            // We only want the function name as the extent for highlighting (and so forth).
            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionDefinitionAst);
            _references.AddReference(
                SymbolType.Function,
                functionDefinitionAst.Name,
                nameExtent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitCommandParameter(CommandParameterAst commandParameterAst)
        {
            _references.AddReference(
                SymbolType.Parameter,
                commandParameterAst.Extent.Text,
                commandParameterAst.Extent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            // TODO: Consider tracking unscoped variable references only when they declared within
            // the same function definition.
            _references.AddReference(
                SymbolType.Variable,
                $"${variableExpressionAst.VariablePath.UserPath}",
                variableExpressionAst.Extent
            );

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitTypeDefinition(TypeDefinitionAst typeDefinitionAst)
        {
            SymbolType symbolType = typeDefinitionAst.IsEnum ? SymbolType.Enum : SymbolType.Class;

            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(typeDefinitionAst);
            _references.AddReference(symbolType, typeDefinitionAst.Name, nameExtent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitTypeExpression(TypeExpressionAst typeExpressionAst)
        {
            _references.AddReference(
                SymbolType.Type,
                typeExpressionAst.TypeName.Name,
                typeExpressionAst.Extent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitTypeConstraint(TypeConstraintAst typeConstraintAst)
        {
            _references.AddReference(SymbolType.Type, typeConstraintAst.TypeName.Name, typeConstraintAst.Extent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitFunctionMember(FunctionMemberAst functionMemberAst)
        {
            SymbolType symbolType = functionMemberAst.IsConstructor
                ? SymbolType.Constructor
                : SymbolType.Method;

            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(functionMemberAst, true, false);
            _references.AddReference(
                symbolType,
                VisitorUtils.GetMemberOverloadName(functionMemberAst, true, false),
                nameExtent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitPropertyMember(PropertyMemberAst propertyMemberAst)
        {
            IScriptExtent nameExtent = VisitorUtils.GetNameExtent(propertyMemberAst, false);
            _references.AddReference(
                SymbolType.Property,
                VisitorUtils.GetMemberOverloadName(propertyMemberAst, false),
                nameExtent);

            return AstVisitAction.Continue;
        }
    }
}
