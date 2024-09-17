// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace PowerShellEditorServices.Test.Shared.Refactoring;
public class RefactorVariableTestCases
{
    public static RenameTestTarget[] TestCases =
    [
        new ("SimpleVariableAssignment.ps1",                   Line: 1,  Column: 1  ),
        new ("VariableRedefinition.ps1",                       Line: 1,  Column: 1  ),
        new ("VariableNestedScopeFunction.ps1",                Line: 1,  Column: 1  ),
        new ("VariableInLoop.ps1",                             Line: 1,  Column: 1  ),
        new ("VariableInPipeline.ps1",                         Line: 23, Column: 2  ),
        new ("VariableInScriptblockScoped.ps1",                Line: 36, Column: 3  ),
        new ("VariablewWithinHastableExpression.ps1",          Line: 46, Column: 3  ),
        new ("VariableNestedFunctionScriptblock.ps1",          Line: 20, Column: 4  ),
        new ("VariableWithinCommandAstScriptBlock.ps1",        Line: 75, Column: 3  ),
        new ("VariableWithinForeachObject.ps1",                Line: 1,  Column: 2  ),
        new ("VariableusedInWhileLoop.ps1",                    Line: 5,  Column: 2  ),
        new ("VariableInParam.ps1",                            Line: 16, Column: 24 ),
        new ("VariableCommandParameter.ps1",                   Line: 9,  Column: 10 ),
        new ("VariableCommandParameter.ps1",                   Line: 17, Column: 3  ),
        new ("VariableScriptWithParamBlock.ps1",               Line: 30, Column: 1  ),
        new ("VariableNonParam.ps1",                           Line: 1,  Column: 7  ),
        new ("VariableParameterCommandWithSameName.ps1",       Line: 13, Column: 9  ),
        new ("VariableCommandParameterSplatted.ps1",           Line: 10, Column: 21 ),
        new ("VariableCommandParameterSplatted.ps1",           Line: 5,  Column: 16 ),
        new ("VariableInForeachDuplicateAssignment.ps1",       Line: 18, Column: 6  ),
        new ("VariableInForloopDuplicateAssignment.ps1",       Line: 14, Column: 9  ),
        new ("VariableNestedScopeFunctionRefactorInner.ps1",   Line: 5,  Column: 3  ),
        new ("VariableSimpleFunctionParameter.ps1",            Line: 9,  Column: 6  ),
        new ("VariableDotNotationFromInnerFunction.ps1",       Line: 26, Column: 11 ),
        new ("VariableDotNotationFromInnerFunction.ps1",       Line: 1,  Column: 1  )
    ];
}
