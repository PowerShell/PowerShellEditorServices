$var = 10
function TestFunction {
    $var = 20
    Write-Output $var
}
TestFunction
Write-Output $var
