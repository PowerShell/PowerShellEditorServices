function OuterFunction {
    function RenamedInnerFunction {
        Write-Host "This is the inner function"
    }
    RenamedInnerFunction
}
OuterFunction
