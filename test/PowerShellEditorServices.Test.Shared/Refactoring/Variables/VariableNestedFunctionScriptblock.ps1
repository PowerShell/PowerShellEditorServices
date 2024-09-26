function Sample{
    $var = 'Hello'
    $sb = {
        Write-Host $var
    }
    & $sb
    $var
}
Sample
