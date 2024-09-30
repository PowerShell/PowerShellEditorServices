// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace PowerShellEditorServices.Test.Shared.Refactoring;

public class RefactorFunctionTestCases
{
    /// <summary>
    /// Defines where functions should be renamed. These numbers are 1-based.
    /// </summary>
    public static RenameTestTarget[] TestCases =
    [
        new("FunctionSimple.ps1",                      Line:  1, Column: 11 ),
        new("FunctionSimple.ps1",                      Line:  1, Column:  1, NoResult: true  ),
        new("FunctionSimple.ps1",                      Line:  2, Column:  4, NoResult: true  ),
        new("FunctionSimple.ps1",                      Line:  1, Column: 11, NewName: "Bad Name", ShouldThrow: true ),
        new("FunctionCallWIthinStringExpression.ps1",  Line:  1, Column: 10 ),
        new("FunctionCmdlet.ps1",                      Line:  1, Column: 10 ),
        new("FunctionForeach.ps1",                     Line: 11, Column:  5 ),
        new("FunctionForeachObject.ps1",               Line: 11, Column:  5 ),
        new("FunctionInnerIsNested.ps1",               Line:  5, Column:  5 ),
        new("FunctionLoop.ps1",                        Line:  5, Column:  5 ),
        new("FunctionMultipleOccurrences.ps1",         Line:  5, Column:  3 ),
        new("FunctionNestedRedefinition.ps1",          Line: 13, Column: 15 ),
        new("FunctionOuterHasNestedFunction.ps1",      Line:  1, Column: 10 ),
        new("FunctionSameName.ps1",                    Line:  3, Column: 14 ),
        new("FunctionScriptblock.ps1",                 Line:  5, Column:  5 ),
        new("FunctionWithInnerFunction.ps1",           Line:  5, Column:  5 ),
        new("FunctionWithInternalCalls.ps1",           Line:  3, Column:  6 ),
    ];
}
