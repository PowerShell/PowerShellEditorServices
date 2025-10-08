function OuterFunction {
    function NewInnerFunction {
        Write-Host "This is the inner function"
    }
    NewInnerFunction
}
OuterFunction
