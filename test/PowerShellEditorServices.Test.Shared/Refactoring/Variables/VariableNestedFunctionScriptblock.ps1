function Sample{
    $var = "Hello"
    $sb = {
        write-host $var
    }
    & $sb
    $var
}
Sample
