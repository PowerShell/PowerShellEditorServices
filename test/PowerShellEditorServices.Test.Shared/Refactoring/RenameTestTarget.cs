// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

namespace PowerShellEditorServices.Test.Shared.Refactoring;

/// <summary>
/// Describes a test case for renaming a file
/// </summary>
/// <param name="FileName">The test case file name e.g. testScript.ps1</param>
/// <param name="Line">The line where the cursor should be positioned for the rename</param>
/// <param name="Column">The column/character indent where ther cursor should be positioned for the rename</param>
/// <param name="NewName">What the target symbol represented by the line and column should be renamed to. Defaults to "Renamed" if not specified</param>
public record RenameTestTarget(string FileName = "UNKNOWN", int Line = -1, int Column = -1, string NewName = "Renamed")
{
    public override string ToString() => $"{FileName.Substring(0, FileName.Length - 4)}";
}
