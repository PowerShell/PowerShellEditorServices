function SameNameFunction {
    Write-Host 'This is the outer function'
    function Renamed {
        Write-Host 'This is the inner function'
    }
    Renamed
}
SameNameFunction
