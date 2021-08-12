class MyClass {
	[String] $Name;
	[Int32] $Number; }
[bool]$scriptBool = $false
$scriptInt = 42
function Test-Variables {
    $strVar = "Hello"
	[string]$strVar2 = "Hello2"
	$arrVar = @(1, 2, $strVar, $objVar)
	$assocArrVar = @{ firstChild = "Child"; secondChild = 42 }
	$classVar = [MyClass]::new();
	$classVar.Name = "Test"
	$classVar.Number = 42;
    $enumVar = $ErrorActionPreference
    $nullString = [NullString]::Value
    $psObjVar = New-Object -TypeName PSObject -Property @{Name = 'John';  Age = 75}
    $psCustomObjVar = [PSCustomObject] @{Name = 'Paul'; Age = 73}
    $procVar = Get-Process -PID $PID
	Write-Output "Done"
}

Test-Variables
# NOTE: If a line is added to the function above, the line numbers in the
# associated unit tests MUST be adjusted accordingly.
