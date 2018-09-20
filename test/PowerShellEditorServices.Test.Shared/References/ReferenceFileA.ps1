. .\ReferenceFileA.ps1
. ./ReferenceFileB.ps1
. .\ReferenceFileC.ps1

function My-Function ($myInput)
{
	My-Function $myInput
}
Get-ChildItem
