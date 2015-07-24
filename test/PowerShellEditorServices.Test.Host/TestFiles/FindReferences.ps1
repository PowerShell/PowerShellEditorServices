function My-Function ($myInput)
{
	My-Function $myInput
}

$things = 4

$things
. simpleps.ps1
My-Function $things

Write-Output "Hi";

Write-Output ""