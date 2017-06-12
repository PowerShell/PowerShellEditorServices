function My-Function ($myInput)
{
    My-Function $myInput
}

$things = 4

$things
My-Function $things

Write-Output "Hi";

Write-Output ""

. .\VariableDefinition.ps1
Write-Output $variableInOtherFile

${variable-with-weird-name} = "this variable has special characters"
Write-Output ${variable-with-weird-name}
