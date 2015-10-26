class MyClass {
	[String] $Name;
	[Int32] $Number;
}

function Test-Variables
{
	$strVar = "Hello"
	$objVar = @{ firstChild = "Child"; secondChild = 42 }
	$arrVar = @(1, 2, $strVar, $objVar)
	$classVar = [MyClass]::new();
	$classVar.Name = "Test"
	$classVar.Number = 42;
	Write-Output "Done"
}

Test-Variables