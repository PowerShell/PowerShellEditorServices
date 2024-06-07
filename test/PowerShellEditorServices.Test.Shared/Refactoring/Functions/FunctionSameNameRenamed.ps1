function SameNameFunction {
    Write-Host "This is the outer function"
    function RenamedSameNameFunction {
        Write-Host "This is the inner function"
    }
    RenamedSameNameFunction
}
SameNameFunction
