$scriptBlock = {
    function Renamed {
        Write-Host "Inside a script block"
    }
    Renamed
}
& $scriptBlock
