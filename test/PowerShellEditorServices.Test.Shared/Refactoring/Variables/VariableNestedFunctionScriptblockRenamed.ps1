function Sample{
    $Renamed = 'Hello'
    $sb = {
        Write-Host $Renamed
    }
    & $sb
    $Renamed
}
Sample
