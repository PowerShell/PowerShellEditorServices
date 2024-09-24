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
    /// <summary>
    /// Finds the most specific Ast at the given script position, or returns null if none found.<br/>
    /// For example, if the position is on a variable expression within a function definition,
    /// the variable will be returned even if the function definition is found first.
    /// </summary>
    internal static Ast? FindAtPosition(this Ast ast, IScriptPosition position, Type[]? allowedTypes)
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

    public static Ast[] FindParents(this Ast ast, params Type[] type)
    {
        List<Ast> parents = new();
        Ast parent = ast;
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

    public static Ast GetHighestParent(this Ast ast)
        => ast.Parent is null ? ast : ast.Parent.GetHighestParent();

    public static Ast GetHighestParent(this Ast ast, params Type[] type)
        => FindParents(ast, type).LastOrDefault() ?? ast;

    /// <summary>
    /// Gets the closest parent that matches the specified type or null if none found.
    /// </summary>
    public static Ast? FindParent(this Ast ast, params Type[] type)
        => FindParents(ast, type).FirstOrDefault();

    /// <summary>
    /// Gets the closest parent that matches the specified type or null if none found.
    /// </summary>
    public static T? FindParent<T>(this Ast ast) where T : Ast
        => ast.FindParent(typeof(T)) as T;

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
