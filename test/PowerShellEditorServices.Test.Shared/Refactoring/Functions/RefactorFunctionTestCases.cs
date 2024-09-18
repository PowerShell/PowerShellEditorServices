// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace PowerShellEditorServices.Test.Shared.Refactoring;

public class RefactorFunctionTestCases
{
    public static RenameTestTarget[] TestCases =
    [
        new("FunctionsSingle.ps1",                     Line: 1,  Column: 11 ),
        new("FunctionMultipleOccurrences.ps1",         Line: 1,  Column:  5 ),
        new("FunctionInnerIsNested.ps1",               Line: 5,  Column:  5, "bar"),
        new("FunctionOuterHasNestedFunction.ps1",      Line: 10, Column:  1 ),
        new("FunctionWithInnerFunction.ps1",           Line: 5,  Column:  5, "RenamedInnerFunction"),
        new("FunctionWithInternalCalls.ps1",           Line: 1,  Column:  5 ),
        new("FunctionCmdlet.ps1",                      Line: 10, Column:  1 ),
        new("FunctionSameName.ps1",                    Line: 14, Column:  3, "RenamedSameNameFunction"),
        new("FunctionScriptblock.ps1",                 Line: 5,  Column:  5 ),
        new("FunctionLoop.ps1",                        Line: 5,  Column:  5 ),
        new("FunctionForeach.ps1",                     Line: 5,  Column: 11 ),
        new("FunctionForeachObject.ps1",               Line: 5,  Column: 11 ),
        new("FunctionCallWIthinStringExpression.ps1",  Line: 1,  Column: 10 ),
        new("FunctionNestedRedefinition.ps1",          Line: 13, Column: 15 )
    ];
}
