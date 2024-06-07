// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.PowerShell.EditorServices.Handlers;

namespace PowerShellEditorServices.Test.Shared.Refactoring.Functions
{
    internal class RefactorsFunctionData
    {

        public static readonly RenameSymbolParams FunctionsSingle = new()
        {
            FileName = "FunctionsSingle.ps1",
            Column = 1,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionMultipleOccurrences = new()
        {
            FileName = "FunctionMultipleOccurrences.ps1",
            Column = 1,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionInnerIsNested = new()
        {
            FileName = "FunctionInnerIsNested.ps1",
            Column = 5,
            Line = 5,
            RenameTo = "bar"
        };
        public static readonly RenameSymbolParams FunctionOuterHasNestedFunction = new()
        {
            FileName = "FunctionOuterHasNestedFunction.ps1",
            Column = 10,
            Line = 1,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionWithInnerFunction = new()
        {
            FileName = "FunctionWithInnerFunction.ps1",
            Column = 5,
            Line = 5,
            RenameTo = "RenamedInnerFunction"
        };
        public static readonly RenameSymbolParams FunctionWithInternalCalls = new()
        {
            FileName = "FunctionWithInternalCalls.ps1",
            Column = 1,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionCmdlet = new()
        {
            FileName = "FunctionCmdlet.ps1",
            Column = 10,
            Line = 1,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionSameName = new()
        {
            FileName = "FunctionSameName.ps1",
            Column = 14,
            Line = 3,
            RenameTo = "RenamedSameNameFunction"
        };
        public static readonly RenameSymbolParams FunctionScriptblock = new()
        {
            FileName = "FunctionScriptblock.ps1",
            Column = 5,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionLoop = new()
        {
            FileName = "FunctionLoop.ps1",
            Column = 5,
            Line = 5,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionForeach = new()
        {
            FileName = "FunctionForeach.ps1",
            Column = 5,
            Line = 11,
            RenameTo = "Renamed"
        };
        public static readonly RenameSymbolParams FunctionForeachObject = new()
        {
            FileName = "FunctionForeachObject.ps1",
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
        public static readonly RenameSymbolParams FunctionNestedRedefinition = new()
        {
            FileName = "FunctionNestedRedefinition.ps1",
            Column = 15,
            Line = 13,
            RenameTo = "Renamed"
        };
    }
}
