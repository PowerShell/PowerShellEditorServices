// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace PowerShellEditorServices.Test.Shared.Refactoring
{
    internal static class RenameUtilitiesData
    {
        public static readonly RenameTestTarget GetVariableExpressionAst = new()
        {
            Column = 11,
            Line = 15,
            NewName = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameTestTarget GetVariableExpressionStartAst = new()
        {
            Column = 1,
            Line = 15,
            NewName = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameTestTarget GetVariableWithinParameterAst = new()
        {
            Column = 21,
            Line = 3,
            NewName = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameTestTarget GetHashTableKey = new()
        {
            Column = 9,
            Line = 16,
            NewName = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameTestTarget GetVariableWithinCommandAst = new()
        {
            Column = 29,
            Line = 6,
            NewName = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameTestTarget GetCommandParameterAst = new()
        {
            Column = 12,
            Line = 21,
            NewName = "Renamed",
            FileName = "TestDetection.ps1"
        };
        public static readonly RenameTestTarget GetFunctionDefinitionAst = new()
        {
            Column = 12,
            Line = 1,
            NewName = "Renamed",
            FileName = "TestDetection.ps1"
        };
    }
}
