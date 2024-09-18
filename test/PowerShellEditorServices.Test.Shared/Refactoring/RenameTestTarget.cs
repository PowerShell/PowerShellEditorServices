// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

namespace PowerShellEditorServices.Test.Shared.Refactoring;

/// <summary>
/// Describes a test case for renaming a file
/// </summary>
public class RenameTestTarget
{
    /// <summary>
    /// The test case file name e.g. testScript.ps1
    /// </summary>
    public string FileName { get; set; } = "UNKNOWN";
    /// <summary>
    /// The line where the cursor should be positioned for the rename
    /// </summary>
    public int Line { get; set; } = -1;
    /// <summary>
    /// The column/character indent where ther cursor should be positioned for the rename
    /// </summary>
    public int Column { get; set; } = -1;
    /// <summary>
    /// What the target symbol represented by the line and column should be renamed to. Defaults to "Renamed" if not specified
    /// </summary>
    public string NewName = "Renamed";

    /// <param name="FileName">The test case file name e.g. testScript.ps1</param>
    /// <param name="Line">The line where the cursor should be positioned for the rename</param>
    /// <param name="Column">The column/character indent where ther cursor should be positioned for the rename</param>
    /// <param name="NewName">What the target symbol represented by the line and column should be renamed to. Defaults to "Renamed" if not specified</param>
    public RenameTestTarget(string FileName, int Line, int Column, string NewName = "Renamed")
    {
        this.FileName = FileName;
        this.Line = Line;
        this.Column = Column;
        this.NewName = NewName;
    }
    public RenameTestTarget() { }

    public override string ToString() => $"{FileName.Substring(0, FileName.Length - 4)}";
}
