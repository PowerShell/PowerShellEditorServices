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
            Column = 23,
            Line = 2,
            RenameTo = "Renamed"
        };
    }
}
