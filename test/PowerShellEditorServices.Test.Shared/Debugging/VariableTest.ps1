class MyClass {
	[String] $Name;
	[Int32] $Number;
}

function Test-Variables
{
	$strVar = "Hello"
	$arrVar = @(1, 2, $strVar, $objVar)
	$assocArrVar = @{ firstChild = "Child"; secondChild = 42 }
	$classVar = [MyClass]::new();
	$classVar.Name = "Test"
	$classVar.Number = 42;
    $enumVar = $ErrorActionPreference
    $psObjVar = New-Object -TypeName PSObject -Property @{Name = 'John';  Age = 75}
    $psCustomObjVar = [PSCustomObject] @{Name = 'Paul'; Age = 73}
    $procVar = Get-Process system
	Write-Output "Done"
}

Test-Variables