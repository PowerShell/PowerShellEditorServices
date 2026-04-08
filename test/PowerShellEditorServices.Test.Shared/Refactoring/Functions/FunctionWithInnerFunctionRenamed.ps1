function OuterFunction {
    function Renamed {
        Write-Host 'This is the inner function'
    }
    Renamed
}
OuterFunction
