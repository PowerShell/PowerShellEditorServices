function Sample{
    $Renamed = "Hello"
    $sb = {
        write-host $Renamed
    }
    & $sb
    $Renamed
}
Sample
