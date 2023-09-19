function RenamedOuterFunction {
    function NewInnerFunction {
        Write-Host "This is the inner function"
    }
    NewInnerFunction
}
RenamedOuterFunction
