// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace PowerShellEditorServices.Test.Shared.Refactoring;
public class RefactorVariableTestCases
{
    public static RenameTestTarget[] TestCases =
    [
        new ("VariableSimpleAssignment.ps1",                   Line:  1, Column:  1),
        new ("VariableSimpleAssignment.ps1",                   Line:  1, Column:  1, NewName: "$Renamed"),
        new ("VariableSimpleAssignment.ps1",                   Line:  1, Column:  1, NewName: "$Bad Name", ShouldThrow: true),
        new ("VariableSimpleAssignment.ps1",                   Line:  1, Column:  1, NewName: "Bad Name", ShouldThrow: true),
        new ("VariableSimpleAssignment.ps1",                   Line:  1, Column:  6, NoResult: true),
        new ("VariableCommandParameter.ps1",                   Line:  3, Column: 17),
        new ("VariableCommandParameter.ps1",                   Line:  3, Column: 17, NewName: "-Renamed"),
        new ("VariableCommandParameter.ps1",                   Line: 10, Column: 10),
        new ("VariableCommandParameterSplatted.ps1",           Line:  3, Column: 19 ),
        new ("VariableCommandParameterSplatted.ps1",           Line: 21, Column: 12),
        new ("VariableDefinedInParamBlock.ps1",                Line: 10, Column:  9),
        new ("VariableDotNotationFromInnerFunction.ps1",       Line:  1, Column:  1),
        new ("VariableDotNotationFromInnerFunction.ps1",       Line: 11, Column: 26),
        new ("VariableInForeachDuplicateAssignment.ps1",       Line:  6, Column: 18),
        new ("VariableInForloopDuplicateAssignment.ps1",       Line:  9, Column: 14),
        new ("VariableInLoop.ps1",                             Line:  1, Column:  1),
        new ("VariableInParam.ps1",                            Line: 24, Column: 16),
        new ("VariableInPipeline.ps1",                         Line:  3, Column: 23),
        new ("VariableInScriptblockScoped.ps1",                Line:  2, Column: 16),
        new ("VariableNestedFunctionScriptblock.ps1",          Line:  4, Column: 20),
        new ("VariableNestedScopeFunction.ps1",                Line:  1, Column:  1),
        new ("VariableNestedScopeFunctionRefactorInner.ps1",   Line:  3, Column:  5),
        new ("VariableNonParam.ps1",                           Line:  7, Column:  1),
        new ("VariableParameterCommandWithSameName.ps1",       Line:  9, Column: 13),
        new ("VariableRedefinition.ps1",                       Line:  1, Column:  1),
        new ("VariableScriptWithParamBlock.ps1",               Line:  1, Column: 30),
        new ("VariableSimpleFunctionParameter.ps1",            Line:  6, Column:  9),
        new ("VariableusedInWhileLoop.ps1",                    Line:  2, Column:  5),
        new ("VariableWithinCommandAstScriptBlock.ps1",        Line:  3, Column: 75),
        new ("VariableWithinForeachObject.ps1",                Line:  2, Column:  1),
        new ("VariableWithinHastableExpression.ps1",           Line:  3, Column: 46),
    ];
}
