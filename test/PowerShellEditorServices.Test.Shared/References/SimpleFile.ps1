function My-Function ($myInput)
{
    My-Function $myInput
}

$things = 4

$things = 3

My-Function $things

Write-Output "Hello World";

Get-ChildItem
gci
dir
Write-Host
Get-ChildItem

My-Alias

Invoke-Command -ScriptBlock ${Function:My-Function}
