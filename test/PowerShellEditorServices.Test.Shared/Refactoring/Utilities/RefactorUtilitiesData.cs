// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Microsoft.PowerShell.EditorServices.Handlers;

namespace PowerShellEditorServices.Test.Shared.Refactoring.Utilities
{
    internal static class RenameUtilitiesData
    {

        public static readonly RenameSymbolParams GetVariableExpressionAst = new()
        {
            Column = 11,
            Line = 15,
            RenameTo = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameSymbolParams GetVariableExpressionStartAst = new()
        {
            Column = 1,
            Line = 15,
            RenameTo = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameSymbolParams GetVariableWithinParameterAst = new()
        {
            Column = 21,
            Line = 3,
            RenameTo = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameSymbolParams GetHashTableKey = new()
        {
            Column = 9,
            Line = 16,
            RenameTo = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameSymbolParams GetVariableWithinCommandAst = new()
        {
            Column = 29,
            Line = 6,
            RenameTo = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameSymbolParams GetCommandParameterAst = new()
        {
            Column = 12,
            Line = 21,
            RenameTo = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameSymbolParams GetFunctionDefinitionAst = new()
        {
            Column = 12,
            Line = 1,
            RenameTo = "Renamed",
            FileName = "TestDetection.ps1"
        };
    }
}
