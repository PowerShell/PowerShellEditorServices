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
        public static readonly RenameSymbolParams VariableWithinCommandAstScriptBlock = new()
        {
            FileName = "VariableWithinCommandAstScriptBlock.ps1",
            Column = 75,
            Line = 3,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableWithinForeachObject = new()
        {
            FileName = "VariableWithinForeachObject.ps1",
            Column = 1,
            Line = 2,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableusedInWhileLoop = new()
        {
            FileName = "VariableusedInWhileLoop.ps1",
            Column = 5,
            Line = 2,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableInParam = new()
        {
            FileName = "VariableInParam.ps1",
            Column = 16,
            Line = 24,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableCommandParameter = new()
        {
            FileName = "VariableCommandParameter.ps1",
            Column = 9,
            Line = 10,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableCommandParameterReverse = new()
        {
            FileName = "VariableCommandParameter.ps1",
            Column = 17,
            Line = 3,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableScriptWithParamBlock = new()
        {
            FileName = "VariableScriptWithParamBlock.ps1",
            Column = 28,
            Line = 1,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableNonParam = new()
        {
            FileName = "VariableNonParam.ps1",
            Column = 1,
            Line = 7,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams VariableParameterCommndWithSameName = new()
        {
            FileName = "VariableParameterCommndWithSameName.ps1",
            Column = 13,
            Line = 9,
            RenameTo = "Renamed"
        };
    }
}
