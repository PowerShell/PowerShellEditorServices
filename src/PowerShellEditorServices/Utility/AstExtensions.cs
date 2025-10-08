// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services;

namespace Microsoft.PowerShell.EditorServices.Language;

// NOTE: A lot of this is reimplementation of https://github.com/PowerShell/PowerShell/blob/2d5d702273060b416aea9601e939ff63bb5679c9/src/System.Management.Automation/engine/parser/Position.cs which is internal and sealed.

public static class AstExtensions
{
    private const int IS_BEFORE = -1;
    private const int IS_AFTER = 1;
    private const int IS_EQUAL = 0;
    internal static int CompareTo(this IScriptPosition position, IScriptPosition other)
    {
        if (position.LineNumber < other.LineNumber)
        {
            return IS_BEFORE;
        }
        else if (position.LineNumber > other.LineNumber)
        {
            return IS_AFTER;
        }
        else //Lines are equal
        {
            if (position.ColumnNumber < other.ColumnNumber)
            {
                return IS_BEFORE;
            }
            else if (position.ColumnNumber > other.ColumnNumber)
            {
                return IS_AFTER;
            }
            else //Columns are equal
            {
                return IS_EQUAL;
            }
        }
    }

    internal static bool IsEqual(this IScriptPosition position, IScriptPosition other)
        => position.CompareTo(other) == IS_EQUAL;

    internal static bool IsBefore(this IScriptPosition position, IScriptPosition other)
        => position.CompareTo(other) == IS_BEFORE;

    internal static bool IsAfter(this IScriptPosition position, IScriptPosition other)
        => position.CompareTo(other) == IS_AFTER;

    internal static bool Contains(this IScriptExtent extent, IScriptPosition position)
        => extent.StartScriptPosition.IsEqual(position)
            || extent.EndScriptPosition.IsEqual(position)
            || (extent.StartScriptPosition.IsBefore(position) && extent.EndScriptPosition.IsAfter(position));

    internal static bool Contains(this IScriptExtent extent, IScriptExtent other)
        => extent.Contains(other.StartScriptPosition) && extent.Contains(other.EndScriptPosition);

    internal static bool StartsBefore(this IScriptExtent extent, IScriptExtent other)
        => extent.StartScriptPosition.IsBefore(other.StartScriptPosition);

    internal static bool StartsBefore(this IScriptExtent extent, IScriptPosition other)
        => extent.StartScriptPosition.IsBefore(other);

    internal static bool StartsAfter(this IScriptExtent extent, IScriptExtent other)
        => extent.StartScriptPosition.IsAfter(other.StartScriptPosition);

    internal static bool StartsAfter(this IScriptExtent extent, IScriptPosition other)
        => extent.StartScriptPosition.IsAfter(other);

    internal static bool IsBefore(this IScriptExtent extent, IScriptExtent other)
        => !other.Contains(extent)
        && !extent.Contains(other)
        && extent.StartScriptPosition.IsBefore(other.StartScriptPosition);

    internal static bool IsAfter(this IScriptExtent extent, IScriptExtent other)
        => !other.Contains(extent)
        && !extent.Contains(other)
        && extent.StartScriptPosition.IsAfter(other.StartScriptPosition);

    internal static bool Contains(this Ast ast, Ast other)
        => ast.Extent.Contains(other.Extent);

    internal static bool Contains(this Ast ast, IScriptPosition position)
        => ast.Extent.Contains(position);

    internal static bool Contains(this Ast ast, IScriptExtent position)
        => ast.Extent.Contains(position);

    internal static bool IsBefore(this Ast ast, Ast other)
        => ast.Extent.IsBefore(other.Extent);

    internal static bool IsAfter(this Ast ast, Ast other)
        => ast.Extent.IsAfter(other.Extent);

    internal static bool StartsBefore(this Ast ast, Ast other)
        => ast.Extent.StartsBefore(other.Extent);

    internal static bool StartsBefore(this Ast ast, IScriptExtent other)
        => ast.Extent.StartsBefore(other);

    internal static bool StartsBefore(this Ast ast, IScriptPosition other)
        => ast.Extent.StartsBefore(other);

    internal static bool StartsAfter(this Ast ast, Ast other)
        => ast.Extent.StartsAfter(other.Extent);

    internal static bool StartsAfter(this Ast ast, IScriptExtent other)
        => ast.Extent.StartsAfter(other);

    internal static bool StartsAfter(this Ast ast, IScriptPosition other)
        => ast.Extent.StartsAfter(other);

    /// <summary>
    /// Finds the outermost Ast that starts before the target and matches the predicate within the scope.
    /// Returns null if none found. Useful for finding definitions of variable/function references.
    /// </summary>
    /// <param name="target">The target Ast to search from</param>
    /// <param name="predicate">The predicate to match the Ast against</param>
    /// <param name="crossScopeBoundaries">If true, the search will continue until the topmost scope boundary is found.</param>
    /// <param name="searchNestedScriptBlocks">Searches scriptblocks within the parent at each level. This can be helpful to find "side" scopes but affects performance</param>
    internal static Ast? FindStartsBefore(this Ast target, Func<Ast, bool> predicate, bool crossScopeBoundaries = false, bool searchNestedScriptBlocks = false)
    {
        Ast? scope = target.GetScopeBoundary();
        do
        {
            Ast? result = scope?.Find(ast =>
                ast.StartsBefore(target)
                && predicate(ast)
            , searchNestedScriptBlocks);

            if (result is not null)
            {
                return result;
            }

            scope = scope?.GetScopeBoundary();
        } while (crossScopeBoundaries && scope is not null);

        return null;
    }

    internal static T? FindStartsBefore<T>(this Ast target, Func<T, bool> predicate, bool crossScopeBoundaries = false, bool searchNestedScriptBlocks = false) where T : Ast
        => target.FindStartsBefore
        (
            ast => ast is T type && predicate(type), crossScopeBoundaries, searchNestedScriptBlocks
        ) as T;

    /// <summary>
    /// Finds all AST items that start before the target and match the predicate within the scope. Items are returned in order from closest to furthest. Returns an empty list if none found. Useful for finding definitions of variable/function references
    /// </summary>
    internal static IEnumerable<Ast> FindAllStartsBefore(this Ast target, Func<Ast, bool> predicate, bool crossScopeBoundaries = false)
    {
        Ast? scope = target.GetScopeBoundary();
        do
        {
            IEnumerable<Ast> results = scope?.FindAll(ast => ast.StartsBefore(target) && predicate(ast)
            , searchNestedScriptBlocks: false) ?? [];

            foreach (Ast result in results.Reverse())
            {
                yield return result;
            }
            scope = scope?.GetScopeBoundary();
        } while (crossScopeBoundaries && scope is not null);
    }

    internal static Ast? FindStartsAfter(this Ast target, Func<Ast, bool> predicate, bool searchNestedScriptBlocks = false)
        => target.Parent.Find(ast => ast.StartsAfter(target) && predicate(ast), searchNestedScriptBlocks);

    internal static IEnumerable<Ast> FindAllStartsAfter(this Ast target, Func<Ast, bool> predicate, bool searchNestedScriptBlocks = false)
        => target.Parent.FindAllStartsAfter(ast => ast.StartsAfter(target) && predicate(ast), searchNestedScriptBlocks);

    /// <summary>
    /// Finds the most specific Ast at the given script position, or returns null if none found.<br/>
    /// For example, if the position is on a variable expression within a function definition,
    /// the variable will be returned even if the function definition is found first, unless variable definitions are not in the list of allowed types
    /// </summary>
    internal static Ast? FindClosest(this Ast ast, IScriptPosition position, Type[]? allowedTypes)
    {
        // Short circuit quickly if the position is not in the provided ast, no need to traverse if not
        if (!ast.Contains(position)) { return null; }

        Ast? mostSpecificAst = null;
        Ast? currentAst = ast;
        do
        {
            currentAst = currentAst.Find(thisAst =>
            {
                // Always starts with the current item, we can skip it
                if (thisAst == mostSpecificAst) { return false; }

                if (allowedTypes is not null && !allowedTypes.Contains(thisAst.GetType())) { return false; }

                if (thisAst.Contains(position))
                {
                    mostSpecificAst = thisAst;
                    return true; //Restart the search within the more specific AST
                }

                return false;
            }, true);
        } while (currentAst is not null);

        return mostSpecificAst;
    }

    public static bool TryFindFunctionDefinition(this Ast ast, CommandAst command, out FunctionDefinitionAst? functionDefinition)
    {
        functionDefinition = ast.FindFunctionDefinition(command);
        return functionDefinition is not null;
    }

    public static FunctionDefinitionAst? FindFunctionDefinition(this Ast ast, CommandAst command)
    {
        if (!ast.Contains(command)) { return null; } // Short circuit if the command is not in the ast

        string? name = command.GetCommandName()?.ToLower();
        if (name is null) { return null; }

        // NOTE: There should only be one match most of the time, the only other cases is when a function is defined multiple times (bad practice). If there are multiple definitions, the candidate "closest" to the command, which would be the last one found, is the appropriate one
        return command.FindAllStartsBefore(ast =>
        {
            if (ast is not FunctionDefinitionAst funcDef) { return false; }

            if (!funcDef.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)) { return false; }

            // If the function is recursive (calls itself), its parent is a match unless a more specific in-scope function definition comes next (this is a "bad practice" edge case)
            if (command.HasParent(funcDef)) { return true; }

            return command.HasParent(funcDef.Parent); // The command is in the same scope as the function definition
        }, true).FirstOrDefault() as FunctionDefinitionAst;
    }

    public static string GetUnqualifiedName(this VariableExpressionAst ast)
        => ast.VariablePath.IsUnqualified
            ? ast.VariablePath.ToString()
            : ast.VariablePath.ToString().Split(':').Last();

    public static Ast GetHighestParent(this Ast ast)
        => ast.Parent is null ? ast : ast.Parent.GetHighestParent();

    public static Ast GetHighestParent(this Ast ast, params Type[] type)
        => FindParents(ast, type).LastOrDefault() ?? ast;

    /// <summary>
    /// Gets the closest parent that matches the specified type or null if none found.
    /// </summary>
    public static T? FindParent<T>(this Ast ast) where T : Ast
        => ast.FindParent(typeof(T)) as T;

    /// <summary>
    /// Gets the closest parent that matches the specified type or null if none found.
    /// </summary>
    public static Ast? FindParent(this Ast ast, params Type[] types)
        => FindParents(ast, types).FirstOrDefault();

    /// <summary>
    /// Returns an enumerable of parents, in order of closest to furthest, that match the specified types.
    /// </summary>
    public static IEnumerable<Ast> FindParents(this Ast ast, params Type[] types)
    {
        Ast parent = ast.Parent;
        while (parent is not null)
        {
            if (types.Contains(parent.GetType()))
            {
                yield return parent;
            }
            parent = parent.Parent;
        }
    }

    /// <summary>
    /// Gets the closest scope boundary of the ast.
    /// </summary>
    public static Ast? GetScopeBoundary(this Ast ast)
        => ast.FindParent
        (
            typeof(ScriptBlockAst),
            typeof(FunctionDefinitionAst),
            typeof(ForEachStatementAst),
            typeof(ForStatementAst)
        );

    public static VariableExpressionAst? FindClosestParameterInFunction(this Ast target, string functionName, string parameterName)
    {
        Ast? scope = target.GetScopeBoundary();
        while (scope is not null)
        {
            FunctionDefinitionAst? funcDef = scope.FindAll
            (
                ast => ast is FunctionDefinitionAst funcDef
                    && funcDef.StartsBefore(target)
                    && funcDef.Name.Equals(functionName, StringComparison.CurrentCultureIgnoreCase)
                    && (funcDef.Parameters ?? funcDef.Body.ParamBlock.Parameters)
                        .SingleOrDefault(
                            param => param.Name.GetUnqualifiedName().Equals(parameterName, StringComparison.CurrentCultureIgnoreCase)
                        ) is not null
                , false
            ).LastOrDefault() as FunctionDefinitionAst;

            if (funcDef is not null)
            {
                return (funcDef.Parameters ?? funcDef.Body.ParamBlock.Parameters)
                    .SingleOrDefault
                    (
                        param => param.Name.GetUnqualifiedName().Equals(parameterName, StringComparison.CurrentCultureIgnoreCase)
                    )?.Name; //Should not be null at this point
            }

            scope = scope.GetScopeBoundary();
        }
        return null;
    }

    /// <summary>
    /// Returns true if the Expression is part of a variable assignment
    /// </summary>
    public static bool IsVariableAssignment(this VariableExpressionAst var)
        => var.Parent is AssignmentStatementAst or ParameterAst;

    public static bool IsOperatorAssignment(this VariableExpressionAst var)
    {
        if (var.Parent is AssignmentStatementAst assignast)
        {
            return assignast.Operator != TokenKind.Equals;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// Returns true if the Ast is a potential variable reference
    /// </summary>
    public static bool IsPotentialVariableReference(this Ast ast)
        => ast is VariableExpressionAst or CommandParameterAst or StringConstantExpressionAst;

    /// <summary>
    /// Determines if a variable assignment is a scoped variable assignment, meaning that it can be considered the top assignment within the current scope. This does not include Variable assignments within the body of a scope which may or may not be the top only if one of these do not exist above it in the same scope.
    /// </summary>
    public static bool IsScopedVariableAssignment(this VariableExpressionAst var)
    {
        // foreach ($x in $y) { }
        if (var.Parent is ForEachStatementAst forEachAst && forEachAst.Variable == var)
        {
            return true;
        }

        // for ($x = 1; $x -lt 10; $x++) { }
        if (var.Parent is ForStatementAst forAst && forAst.Initializer is AssignmentStatementAst assignAst && assignAst.Left == var)
        {
            return true;
        }

        // param($x = 1)
        if (var.Parent is ParameterAst paramAst && paramAst.Name == var)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// For a given string constant, determine if it is a splat, and there is at least one splat reference. If so, return the location of the splat assignment.
    /// </summary>
    public static VariableExpressionAst? FindSplatParameterReference(this StringConstantExpressionAst stringConstantAst)
    {
        if (stringConstantAst.Parent is not HashtableAst hashtableAst) { return null; }
        if (hashtableAst.Parent is not CommandExpressionAst commandAst) { return null; }
        if (commandAst.Parent is not AssignmentStatementAst assignmentAst) { return null; }
        if (assignmentAst.Left is not VariableExpressionAst leftAssignVarAst) { return null; }
        return assignmentAst.FindStartsAfter(ast =>
            ast is VariableExpressionAst var
            && var.Splatted
            && var.GetUnqualifiedName().Equals(leftAssignVarAst.GetUnqualifiedName(), StringComparison.CurrentCultureIgnoreCase)
        , true) as VariableExpressionAst;
    }

    /// <summary>
    /// For a given splat reference, find its source splat assignment. If the reference is not a splat, an exception will be thrown. If no assignment is found, null will be returned.
    /// </summary>
    public static StringConstantExpressionAst? FindSplatAssignmentReference(this VariableExpressionAst varAst)
    {
        if (!varAst.Splatted) { throw new InvalidOperationException("The provided variable reference is not a splat and cannot be used with FindSplatVariableAssignment"); }

        return varAst.FindStartsBefore(ast =>
            ast is StringConstantExpressionAst stringAst
            && stringAst.Value == varAst.GetUnqualifiedName()
            && stringAst.FindSplatParameterReference() == varAst,
            crossScopeBoundaries: true) as StringConstantExpressionAst;
    }

    /// <summary>
    /// Returns the function a parameter is defined in. Returns null if it is an anonymous function such as a scriptblock
    /// </summary>
    public static bool TryGetFunction(this ParameterAst ast, out FunctionDefinitionAst? function)
    {
        if (ast.Parent is FunctionDefinitionAst funcDef) { function = funcDef; return true; }
        if (ast.Parent.Parent is FunctionDefinitionAst paramBlockFuncDef) { function = paramBlockFuncDef; return true; }
        function = null;
        return false;
    }

    /// <summary>
    /// Finds the highest variable expression within a variable assignment within the current scope of the provided variable reference. Returns the original object if it is the highest assignment or null if no assignment was found. It is assumed the reference is part of a larger Ast.
    /// </summary>
    /// <param name="reference">A variable reference that is either a VariableExpression or a StringConstantExpression (splatting reference)</param>
    public static Ast? GetTopVariableAssignment(this Ast reference)
    {
        if (!reference.IsPotentialVariableReference())
        {
            throw new NotSupportedException("The provided reference is not a variable reference type.");
        }

        // Splats are special, we will treat them as a top variable assignment and search both above for a parameter assignment and below for a splat reference, but we don't require a command definition within the same scope for the splat.
        if (reference is StringConstantExpressionAst stringConstant)
        {
            VariableExpressionAst? splat = stringConstant.FindSplatParameterReference();
            if (splat is null) { return null; }
            // Find the function associated with the splat parameter reference
            string? commandName = (splat.Parent as CommandAst)?.GetCommandName().ToLower();
            if (commandName is null) { return null; }
            VariableExpressionAst? splatParamReference = splat.FindClosestParameterInFunction(commandName, stringConstant.Value);

            if (splatParamReference is not null)
            {
                return splatParamReference;
            }
        }

        // If nothing found, search parent scopes for a variable assignment until we hit the top of the document
        string name = reference switch
        {
            VariableExpressionAst varExpression => varExpression.GetUnqualifiedName(),
            CommandParameterAst param => param.ParameterName,
            StringConstantExpressionAst stringConstantExpressionAst => stringConstantExpressionAst.Value,
            _ => throw new NotSupportedException("The provided reference is not a variable reference type.")
        };
        VariableExpressionAst? varAssignment = null;
        Ast? scope = reference;

        while (scope is not null)
        {
            // Check if the reference is a parameter in the current scope. This saves us from having to do a nested search later on.
            IEnumerable<ParameterAst>? parameters = scope switch
            {
                // Covers both function test() { param($x) } and function param($x)
                FunctionDefinitionAst f => f.Body?.ParamBlock?.Parameters ?? f.Parameters,
                ScriptBlockAst s => s.ParamBlock?.Parameters,
                _ => null
            };
            ParameterAst? matchParam = parameters?.SingleOrDefault(
                param => param.Name.GetUnqualifiedName().Equals(name, StringComparison.CurrentCultureIgnoreCase)
            );
            if (matchParam is not null)
            {
                return matchParam.Name;
            }

            // Find any top level function definitions in the currentscope that might match the parameter
            if (reference is CommandParameterAst parameterAst)
            {
                string? commandName = (parameterAst.Parent as CommandAst)?.GetCommandName()?.ToLower();

                if (commandName is not null)
                {
                    VariableExpressionAst? paramDefinition = parameterAst.FindClosestParameterInFunction(commandName, parameterAst.ParameterName);
                    if (paramDefinition is not null)
                    {
                        return paramDefinition;
                    }
                }
            }

            // Will find the outermost assignment within the scope that matches the reference.
            varAssignment = reference switch
            {
                VariableExpressionAst => scope.FindStartsBefore<VariableExpressionAst>(var =>
                    var.GetUnqualifiedName().Equals(name, StringComparison.CurrentCultureIgnoreCase)
                    && (
                        (var.IsVariableAssignment() && !var.IsOperatorAssignment())
                        || var.IsScopedVariableAssignment()
                    )
                    , crossScopeBoundaries: false, searchNestedScriptBlocks: false
                ),

                CommandParameterAst param => scope.FindStartsBefore<VariableExpressionAst>(var =>
                    var.GetUnqualifiedName().Equals(name, StringComparison.CurrentCultureIgnoreCase)
                    && var.Parent is ParameterAst paramAst
                    && paramAst.TryGetFunction(out FunctionDefinitionAst? foundFunction)
                    && foundFunction?.Name.ToLower()
                        == (param.Parent as CommandAst)?.GetCommandName()?.ToLower()
                    && foundFunction?.Parent?.Parent == scope
                ),

                _ => null
            };

            if (varAssignment is not null)
            {
                return varAssignment;
            }

            if (reference is VariableExpressionAst varAst
                &&
                (
                    varAst.IsScopedVariableAssignment()
                    || (varAst.IsVariableAssignment() && !varAst.IsOperatorAssignment())
                )
            )
            {
                // The current variable reference is the top level assignment because we didn't find any other assignments above it
                return reference;
            }

            // Get the next highest scope
            scope = scope.GetScopeBoundary();
        }

        // If we make it this far we didn't find any references.

        // An operator assignment can be a definition only as long as there are no assignments above it in all scopes.
        if (reference is VariableExpressionAst variableAst
            && variableAst.IsVariableAssignment()
            && variableAst.IsOperatorAssignment())
        {
            return reference;
        }

        return null;
    }

    public static bool HasParent(this Ast ast, Ast parent)
    {
        Ast? current = ast;
        while (current is not null)
        {
            if (current == parent)
            {
                return true;
            }
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Return an extent that only contains the position of the name of the function, for Client highlighting purposes.
    /// </summary>
    internal static ScriptExtentAdapter GetFunctionNameExtent(this FunctionDefinitionAst ast)
    {
        string name = ast.Name;
        // FIXME: Gather dynamically from the AST and include backticks and whatnot that might be present
        int funcLength = "function ".Length;
        ScriptExtentAdapter funcExtent = new(ast.Extent);
        funcExtent.Start = funcExtent.Start.Delta(0, funcLength);
        funcExtent.End = funcExtent.Start.Delta(0, name.Length);

        return funcExtent;
    }
}
