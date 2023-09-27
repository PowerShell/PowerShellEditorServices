// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.PowerShell.EditorServices.Handlers;

namespace PowerShellEditorServices.Test.Shared.Refactoring.Variables
{
    internal static class RenameVariableData
    {

        public static readonly RenameSymbolParams SimpleVariableAssignment = new()
        {
            FileName = "SimpleVariableAssignment.ps1",
            Column = 1,
            Line = 1,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableRedefinition = new()
        {
            FileName = "VariableRedefinition.ps1",
            Column = 1,
            Line = 1,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableNestedScopeFunction = new()
        {
            FileName = "VariableNestedScopeFunction.ps1",
            Column = 1,
            Line = 1,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableInLoop = new()
        {
            FileName = "VariableInLoop.ps1",
            Column = 1,
            Line = 1,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableInPipeline = new()
        {
            FileName = "VariableInPipeline.ps1",
            Column = 23,
            Line = 2,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableInScriptblock = new()
        {
            FileName = "VariableInScriptblock.ps1",
            Column = 26,
            Line = 2,
            RenameTo = "Renamed"
        };

        public static readonly RenameSymbolParams VariableInScriptblockScoped = new()
        {
            FileName = "VariableInScriptblockScoped.ps1",
            Column = 36,
            Line = 2,
            RenameTo = "Renamed"
        };

        public static readonly RenameSymbolParams VariablewWithinHastableExpression = new()
        {
            FileName = "VariablewWithinHastableExpression.ps1",
            Column = 46,
            Line = 3,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableNestedFunctionScriptblock = new()
        {
            FileName = "VariableNestedFunctionScriptblock.ps1",
            Column = 20,
            Line = 4,
            RenameTo = "Renamed"
        };
    }
}
