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
    $psObjVar = New-Object -TypeName PSObject -Property @{Name = 'John'; Age = 75 }
    $psCustomObjVar = [PSCustomObject] @{Name = 'Paul'; Age = 73 }
    $procVar = Get-Process -PID $PID
    $trueVar = $true
    $falseVar = $false
    Write-Output "Done"
}

Test-Variables
# NOTE: If a line is added to the function above, the line numbers in the
# associated unit tests MUST be adjusted accordingly.

$SCRIPT:simpleArray = @(
    1
    2
    'red'
    'blue'
)

# This is a dummy function that the test will use to stop and evaluate the debug environment
function __BreakDebuggerEnumerableShowsRawView{}; __BreakDebuggerEnumerableShowsRawView

$SCRIPT:simpleDictionary = @{
    item1 = 1
    item2 = 2
    item3 = 'red'
    item4 = 'blue'
}
function __BreakDebuggerDictionaryShowsRawView{}; __BreakDebuggerDictionaryShowsRawView

$SCRIPT:sortedDictionary = [Collections.Generic.SortedDictionary[string, object]]::new()
$sortedDictionary[1] = 1
$sortedDictionary[2] = 2
$sortedDictionary['red'] = 'red'
$sortedDictionary['blue'] = 'red'

# This is a dummy function that the test will use to stop and evaluate the debug environment
function __BreakDebuggerDerivedDictionaryPropertyInRawView{}; __BreakDebuggerDerivedDictionaryPropertyInRawView

class CustomToString {
    [String]$String = 'Hello'
    [String]ToString() {
        return $this.String.ToUpper()
    }
}
$SCRIPT:CustomToStrings = 1..1000 | ForEach-Object {
    [CustomToString]::new()
}

# This is a dummy function that the test will use to stop and evaluate the debug environment
function __BreakDebuggerToStringShouldMarshallToPipeline{}; __BreakDebuggerToStringShouldMarshallToPipeline
