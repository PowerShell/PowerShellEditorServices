// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.PowerShell.EditorServices.Handlers;

namespace PowerShellEditorServices.Test.Shared.Refactoring.Functions
{
    internal static class RefactorsFunctionData
    {

        public static readonly RenameSymbolParams FunctionsSingle = new()
        {
            FileName = "BasicFunction.ps1",
            Column = 1,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionMultipleOccurrences = new()
        {
            FileName = "MultipleOccurrences.ps1",
            Column = 1,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionInnerIsNested = new()
        {
            FileName = "NestedFunctions.ps1",
            Column = 5,
            Line = 5,
            RenameTo = "bar"
        };
        public static readonly RenameSymbolParams FunctionOuterHasNestedFunction = new()
        {
            FileName = "OuterFunction.ps1",
            Column = 10,
            Line = 1,
            RenameTo = "RenamedOuterFunction"
        };
        public static readonly RenameSymbolParams FunctionWithInnerFunction = new()
        {
            FileName = "InnerFunction.ps1",
            Column = 5,
            Line = 5,
            RenameTo = "RenamedInnerFunction"
        };
        public static readonly RenameSymbolParams FunctionWithInternalCalls = new()
        {
            FileName = "InternalCalls.ps1",
            Column = 1,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionCmdlet = new()
        {
            FileName = "CmdletFunction.ps1",
            Column = 10,
            Line = 1,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionSameName = new()
        {
            FileName = "SamenameFunctions.ps1",
            Column = 14,
            Line = 3,
            RenameTo = "RenamedSameNameFunction"
        };
        public static readonly RenameSymbolParams FunctionScriptblock = new()
        {
            FileName = "ScriptblockFunction.ps1",
            Column = 5,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionLoop = new()
        {
            FileName = "LoopFunction.ps1",
            Column = 5,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionForeach = new()
        {
            FileName = "ForeachFunction.ps1",
            Column = 5,
            Line = 11,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionForeachObject = new()
        {
            FileName = "ForeachObjectFunction.ps1",
            Column = 5,
            Line = 11,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionCallWIthinStringExpression = new()
        {
            FileName = "FunctionCallWIthinStringExpression.ps1",
            Column = 10,
            Line = 1,
            RenameTo = "Renamed"
        };
    }
}
