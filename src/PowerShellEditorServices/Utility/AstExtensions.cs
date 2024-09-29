// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Services;

namespace Microsoft.PowerShell.EditorServices.Language;

public static class AstExtensions
{

    internal static bool Contains(this Ast ast, Ast other) => ast.Find(ast => ast == other, true) != null;
    internal static bool Contains(this Ast ast, IScriptPosition position) => new ScriptExtentAdapter(ast.Extent).Contains(position);

    internal static bool IsAfter(this Ast ast, Ast other)
    {
        return
            ast.Extent.StartLineNumber > other.Extent.EndLineNumber
            ||
            (
                ast.Extent.StartLineNumber == other.Extent.EndLineNumber
                && ast.Extent.StartColumnNumber > other.Extent.EndColumnNumber
            );
    }

    internal static bool IsBefore(this Ast ast, Ast other)
    {
        return
            ast.Extent.EndLineNumber < other.Extent.StartLineNumber
            ||
            (
                ast.Extent.EndLineNumber == other.Extent.StartLineNumber
                && ast.Extent.EndColumnNumber < other.Extent.StartColumnNumber
            );
    }

    internal static bool StartsBefore(this Ast ast, Ast other)
    {
        return
            ast.Extent.StartLineNumber < other.Extent.StartLineNumber
            ||
            (
                ast.Extent.StartLineNumber == other.Extent.StartLineNumber
                && ast.Extent.StartColumnNumber < other.Extent.StartColumnNumber
            );
    }

    internal static bool StartsAfter(this Ast ast, Ast other)
    {
        return
            ast.Extent.StartLineNumber < other.Extent.StartLineNumber
            ||
            (
                ast.Extent.StartLineNumber == other.Extent.StartLineNumber
                && ast.Extent.StartColumnNumber < other.Extent.StartColumnNumber
            );
    }

    internal static Ast? FindBefore(this Ast target, Func<Ast, bool> predicate, bool crossScopeBoundaries = false)
    {
        Ast? scope = crossScopeBoundaries
            ? target.FindParents(typeof(ScriptBlockAst)).LastOrDefault()
            : target.GetScopeBoundary();
        return scope?.Find(ast => ast.IsBefore(target) && predicate(ast), false);
    }

    internal static IEnumerable<Ast> FindAllBefore(this Ast target, Func<Ast, bool> predicate, bool crossScopeBoundaries = false)
    {
        Ast? scope = crossScopeBoundaries
            ? target.FindParents(typeof(ScriptBlockAst)).LastOrDefault()
            : target.GetScopeBoundary();
        return scope?.FindAll(ast => ast.IsBefore(target) && predicate(ast), false) ?? [];
    }

    internal static Ast? FindAfter(this Ast target, Func<Ast, bool> predicate, bool crossScopeBoundaries = false)
        => target.Parent.Find(ast => ast.IsAfter(target) && predicate(ast), crossScopeBoundaries);

    internal static IEnumerable<Ast> FindAllAfter(this Ast target, Func<Ast, bool> predicate, bool crossScopeBoundaries = false)
        => target.Parent.FindAll(ast => ast.IsAfter(target) && predicate(ast), crossScopeBoundaries);

    /// <summary>
    /// Finds the most specific Ast at the given script position, or returns null if none found.<br/>
    /// For example, if the position is on a variable expression within a function definition,
    /// the variable will be returned even if the function definition is found first, unless variable definitions are not in the list of allowed types
    /// </summary>
    internal static Ast? FindClosest(this Ast ast, IScriptPosition position, Type[]? allowedTypes)
    {
        // Short circuit quickly if the position is not in the provided range, no need to traverse if not
        // TODO: Maybe this should be an exception instead? I mean technically its not found but if you gave a position outside the file something very wrong probably happened.
        if (!new ScriptExtentAdapter(ast.Extent).Contains(position)) { return null; }

        // This will be updated with each loop, and re-Find to dig deeper
        Ast? mostSpecificAst = null;
        Ast? currentAst = ast;

        do
        {
            currentAst = currentAst.Find(thisAst =>
            {
                if (thisAst == mostSpecificAst) { return false; }

                int line = position.LineNumber;
                int column = position.ColumnNumber;

                // Performance optimization, skip statements that don't contain the position
                if (
                    thisAst.Extent.EndLineNumber < line
                    || thisAst.Extent.StartLineNumber > line
                    || (thisAst.Extent.EndLineNumber == line && thisAst.Extent.EndColumnNumber < column)
                    || (thisAst.Extent.StartLineNumber == line && thisAst.Extent.StartColumnNumber > column)
                )
                {
                    return false;
                }

                if (allowedTypes is not null && !allowedTypes.Contains(thisAst.GetType()))
                {
                    return false;
                }

                if (new ScriptExtentAdapter(thisAst.Extent).Contains(position))
                {
                    mostSpecificAst = thisAst;
                    return true; //Stops this particular find and looks more specifically
                }

                return false;
            }, true);

            if (currentAst is not null)
            {
                mostSpecificAst = currentAst;
            }
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
        string? name = command.GetCommandName()?.ToLower();
        if (name is null) { return null; }

        FunctionDefinitionAst[] candidateFuncDefs = ast.FindAll(ast =>
        {
            if (ast is not FunctionDefinitionAst funcDef)
            {
                return false;
            }

            if (funcDef.Name.ToLower() != name)
            {
                return false;
            }

            // If the function is recursive (calls itself), its parent is a match unless a more specific in-scope function definition comes next (this is a "bad practice" edge case)
            // TODO: Consider a simple "contains" match
            if (command.HasParent(funcDef))
            {
                return true;
            }

            if
            (
                // TODO: Replace with a position match
                funcDef.Extent.EndLineNumber > command.Extent.StartLineNumber
                ||
                (
                    funcDef.Extent.EndLineNumber == command.Extent.StartLineNumber
                    && funcDef.Extent.EndColumnNumber >= command.Extent.StartColumnNumber
                )
            )
            {
                return false;
            }

            return command.HasParent(funcDef.Parent); // The command is in the same scope as the function definition
        }, true).Cast<FunctionDefinitionAst>().ToArray();

        // There should only be one match most of the time, the only other cases is when a function is defined multiple times (bad practice). If there are multiple definitions, the candidate "closest" to the command, which would be the last one found, is the appropriate one
        return candidateFuncDefs.LastOrDefault();
    }

    public static string GetUnqualifiedName(this VariableExpressionAst ast)
        => ast.VariablePath.IsUnqualified
            ? ast.VariablePath.ToString()
            : ast.VariablePath.ToString().Split(':').Last();

    /// <summary>
    /// Finds the closest variable definition to the given reference.
    /// </summary>
    public static VariableExpressionAst? FindVariableDefinition(this Ast ast, Ast reference)
    {
        string? name = reference switch
        {
            VariableExpressionAst var => var.GetUnqualifiedName(),
            CommandParameterAst param => param.ParameterName,
            // StringConstantExpressionAst stringConstant => ,
            _ => null
        };
        if (name is null) { return null; }

        return ast.FindAll(candidate =>
        {
            if (candidate is not VariableExpressionAst candidateVar) { return false; }
            if (candidateVar.GetUnqualifiedName() != name) { return false; }
            if
            (
                // TODO: Replace with a position match
                candidateVar.Extent.EndLineNumber > reference.Extent.StartLineNumber
                ||
                (
                    candidateVar.Extent.EndLineNumber == reference.Extent.StartLineNumber
                    && candidateVar.Extent.EndColumnNumber >= reference.Extent.StartColumnNumber
                )
            )
            {
                return false;
            }

            return candidateVar.HasParent(reference.Parent);
        }, true).Cast<VariableExpressionAst>().LastOrDefault();
    }

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
    public static Ast? FindParent(this Ast ast, params Type[] type)
        => FindParents(ast, type).FirstOrDefault();

    /// <summary>
    /// Returns an array of parents in order from closest to furthest
    /// </summary>
    public static Ast[] FindParents(this Ast ast, params Type[] type)
    {
        List<Ast> parents = new();
        Ast parent = ast.Parent;
        while (parent is not null)
        {
            if (type.Contains(parent.GetType()))
            {
                parents.Add(parent);
            }
            parent = parent.Parent;
        }
        return parents.ToArray();
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

    /// <summary>
    /// Returns true if the Expression is part of a variable assignment
    /// </summary>
    /// TODO: Potentially check the name matches
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
    // TODO: Naming is hard, I feel like this could have a better name
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
    /// For a given string constant, determine if it is a splat, and there is at least one splat reference. If so, return a tuple of the variable assignment and the name of the splat reference. If not, return null.
    /// </summary>
    public static VariableExpressionAst? FindSplatVariableAssignment(this StringConstantExpressionAst stringConstantAst)
    {
        if (stringConstantAst.Parent is not HashtableAst hashtableAst) { return null; }
        if (hashtableAst.Parent is not CommandExpressionAst commandAst) { return null; }
        if (commandAst.Parent is not AssignmentStatementAst assignmentAst) { return null; }
        if (assignmentAst.Left is not VariableExpressionAst leftAssignVarAst) { return null; }
        return assignmentAst.FindAfter(ast =>
            ast is VariableExpressionAst var
            && var.Splatted
            && var.GetUnqualifiedName().ToLower() == leftAssignVarAst.GetUnqualifiedName().ToLower()
        , true) as VariableExpressionAst;
    }

    /// <summary>
    /// For a given splat reference, find its source splat assignment. If the reference is not a splat, an exception will be thrown. If no assignment is found, null will be returned.
    /// TODO: Support incremental splat references e.g. $x = @{}, $x.Method = 'GET'
    /// </summary>
    public static StringConstantExpressionAst? FindSplatAssignmentReference(this VariableExpressionAst varAst)
    {
        if (!varAst.Splatted) { throw new InvalidOperationException("The provided variable reference is not a splat and cannot be used with FindSplatVariableAssignment"); }

        return varAst.FindBefore(ast =>
            ast is StringConstantExpressionAst stringAst
            && stringAst.Value == varAst.GetUnqualifiedName()
            && stringAst.FindSplatVariableAssignment() == varAst,
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
            VariableExpressionAst? splat = stringConstant.FindSplatVariableAssignment();
            if (splat is not null)
            {
                return reference;
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

        Ast? scope = reference.GetScopeBoundary();

        VariableExpressionAst? varAssignment = null;

        while (scope is not null)
        {
            // Check if the reference is a parameter in the current scope. This saves us from having to do a nested search later on.
            // TODO: Can probably be combined with below
            IEnumerable<ParameterAst>? parameters = scope switch
            {
                // Covers both function test() { param($x) } and function param($x)
                FunctionDefinitionAst f => f.Body?.ParamBlock?.Parameters ?? f.Parameters,
                ScriptBlockAst s => s.ParamBlock?.Parameters,
                _ => null
            };
            ParameterAst? matchParam = parameters?.SingleOrDefault(
                param => param.Name.GetUnqualifiedName().ToLower() == name.ToLower()
            );
            if (matchParam is not null)
            {
                return matchParam.Name;
            }

            // Find any top level function definitions in the currentscope that might match the parameter
            // TODO: This could be less complicated
            if (reference is CommandParameterAst parameterAst)
            {
                FunctionDefinitionAst? closestFunctionMatch = scope.FindAll(
                    ast => ast is FunctionDefinitionAst funcDef
                    && funcDef.Name.ToLower() == (parameterAst.Parent as CommandAst)?.GetCommandName()?.ToLower()
                    && (funcDef.Parameters ?? funcDef.Body.ParamBlock.Parameters).SingleOrDefault(
                        param => param.Name.GetUnqualifiedName().ToLower() == name.ToLower()
                    ) is not null
                    , false
                ).LastOrDefault() as FunctionDefinitionAst;

                if (closestFunctionMatch is not null)
                {
                    //TODO: This should not ever be null but should probably be sure.
                    return
                    (closestFunctionMatch.Parameters ?? closestFunctionMatch.Body.ParamBlock.Parameters)
                    .SingleOrDefault
                    (
                        param => param.Name.GetUnqualifiedName().ToLower() == name.ToLower()
                    )?.Name;
                };
            };

            // Will find the outermost assignment that matches the reference.
            varAssignment = reference switch
            {
                VariableExpressionAst var => scope.Find
                (
                    ast => ast is VariableExpressionAst var
                            && ast.IsBefore(reference)
                            &&
                            (
                                (var.IsVariableAssignment() && !var.IsOperatorAssignment())
                                || var.IsScopedVariableAssignment()
                            )
                            && var.GetUnqualifiedName().ToLower() == name.ToLower()
                    , searchNestedScriptBlocks: false
                ) as VariableExpressionAst,

                CommandParameterAst param => scope.Find
                (
                    ast => ast is VariableExpressionAst var
                            && ast.IsBefore(reference)
                            && var.GetUnqualifiedName().ToLower() == name.ToLower()
                            && var.Parent is ParameterAst paramAst
                            && paramAst.TryGetFunction(out FunctionDefinitionAst? foundFunction)
                            && foundFunction?.Name.ToLower()
                                == (param.Parent as CommandAst)?.GetCommandName()?.ToLower()
                            && foundFunction?.Parent?.Parent == scope
                    , searchNestedScriptBlocks: true //This might hit side scopes...
                ) as VariableExpressionAst,
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

    public static bool WithinScope(this Ast Target, Ast Child)
    {
        Ast childParent = Child.Parent;
        Ast? TargetScope = Target.GetScopeBoundary();
        while (childParent != null)
        {
            if (childParent is FunctionDefinitionAst FuncDefAst)
            {
                if (Child is VariableExpressionAst VarExpAst && !IsVariableExpressionAssignedInTargetScope(VarExpAst, FuncDefAst))
                {

                }
                else
                {
                    break;
                }
            }
            if (childParent == TargetScope)
            {
                break;
            }
            childParent = childParent.Parent;
        }
        return childParent == TargetScope;
    }

    public static bool IsVariableExpressionAssignedInTargetScope(this VariableExpressionAst node, Ast scope)
    {
        bool r = false;

        List<VariableExpressionAst> VariableAssignments = node.FindAll(ast =>
        {
            return ast is VariableExpressionAst VarDef &&
            VarDef.Parent is AssignmentStatementAst or ParameterAst &&
            VarDef.VariablePath.UserPath.ToLower() == node.VariablePath.UserPath.ToLower() &&
            // Look Backwards from the node above
            (VarDef.Extent.EndLineNumber < node.Extent.StartLineNumber ||
            (VarDef.Extent.EndColumnNumber <= node.Extent.StartColumnNumber &&
            VarDef.Extent.EndLineNumber <= node.Extent.StartLineNumber)) &&
            // Must be within the the designated scope
            VarDef.Extent.StartLineNumber >= scope.Extent.StartLineNumber;
        }, true).Cast<VariableExpressionAst>().ToList();

        if (VariableAssignments.Count > 0)
        {
            r = true;
        }
        // Node is probably the first Assignment Statement within scope
        if (node.Parent is AssignmentStatementAst && node.Extent.StartLineNumber >= scope.Extent.StartLineNumber)
        {
            r = true;
        }

        return r;
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

}
