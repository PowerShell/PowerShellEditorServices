function SameNameFunction {
    Write-Host "This is the outer function"
    function SameNameFunction {
        Write-Host "This is the inner function"
    }
    SameNameFunction
}
SameNameFunction
