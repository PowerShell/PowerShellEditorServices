// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.PowerShell.EditorServices.Handlers;

namespace PowerShellEditorServices.Test.Shared.Refactoring
{
    internal static class RefactorsFunctionData
    {
        public static readonly RenameSymbolParams FunctionsMultiple = new()
        {
            // rename function Two { ...}
            FileName = "FunctionsMultiple.ps1",
            Column = 9,
            Line = 3,
            RenameTo = "TwoFours"
        };
        public static readonly RenameSymbolParams FunctionsMultipleFromCommandDef = new()
        {
            //  ... write-host "Three Hello" ...
            // Two
            //
            FileName = "FunctionsMultiple.ps1",
            Column = 5,
            Line = 15,
            RenameTo = "OnePlusOne"
        };
        public static readonly RenameSymbolParams FunctionsSingleParams = new()
        {
            FileName = "FunctionsSingle.ps1",
            Column = 9,
            Line = 0,
            RenameTo = "OneMethod"
        };
        public static readonly RenameSymbolParams FunctionsSingleNested = new()
        {
            FileName = "FunctionsNestedSimple.ps1",
            Column = 16,
            Line = 4,
            RenameTo = "OneMethod"
        };
        public static readonly RenameSymbolParams FunctionsSimpleFlat = new()
        {
            FileName = "FunctionsFlat.ps1",
            Column = 81,
            Line = 0,
            RenameTo = "ChangedFlat"
        };
    }
}
