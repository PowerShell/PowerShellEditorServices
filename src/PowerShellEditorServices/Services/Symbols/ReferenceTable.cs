// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.Symbols;

namespace Microsoft.PowerShell.EditorServices.Services;

/// <summary>
/// Represents the symbols that are referenced and their locations within a single document.
/// </summary>
internal sealed class ReferenceTable
{
    private readonly ScriptFile _parent;

    private readonly ConcurrentDictionary<string, ConcurrentBag<IScriptExtent>> _symbolReferences = new(StringComparer.OrdinalIgnoreCase);

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

    internal bool TryGetReferences(string command, out ConcurrentBag<IScriptExtent>? references)
    {
        EnsureInitialized();
        return _symbolReferences.TryGetValue(command, out references);
    }

    internal void EnsureInitialized()
    {
        if (IsInitialized)
        {
            return;
        }

        _parent.ScriptAst.Visit(new ReferenceVisitor(this));
    }

    private void AddReference(string symbol, IScriptExtent extent)
    {
        _symbolReferences.AddOrUpdate(
            symbol,
            _ => new ConcurrentBag<IScriptExtent> { extent },
            (_, existing) =>
            {
                existing.Add(extent);
                return existing;
            });
    }

    private sealed class ReferenceVisitor : AstVisitor
    {
        private readonly ReferenceTable _references;

        public ReferenceVisitor(ReferenceTable references) => _references = references;

        private static string? GetCommandName(CommandAst commandAst)
        {
            string commandName = commandAst.GetCommandName();
            if (!string.IsNullOrEmpty(commandName))
            {
                return commandName;
            }

            if (commandAst.CommandElements[0] is not ExpandableStringExpressionAst expandableStringExpressionAst)
            {
                return null;
            }

            return AstOperations.TryGetInferredValue(expandableStringExpressionAst, out string value) ? value : null;
        }

        public override AstVisitAction VisitCommand(CommandAst commandAst)
        {
            string? commandName = GetCommandName(commandAst);
            if (string.IsNullOrEmpty(commandName))
            {
                return AstVisitAction.Continue;
            }

            _references.AddReference(
                CommandHelpers.StripModuleQualification(commandName, out _),
                commandAst.CommandElements[0].Extent);

            return AstVisitAction.Continue;
        }

        public override AstVisitAction VisitVariableExpression(VariableExpressionAst variableExpressionAst)
        {
            // TODO: Consider tracking unscoped variable references only when they declared within
            // the same function definition.
            _references.AddReference(
                $"${variableExpressionAst.VariablePath.UserPath}",
                variableExpressionAst.Extent);

            return AstVisitAction.Continue;
        }
    }
}
