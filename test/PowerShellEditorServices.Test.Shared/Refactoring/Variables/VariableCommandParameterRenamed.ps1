function Get-foo {
    param (
        [string][Alias("string")]$Renamed,
        [int]$pos
    )

    return $Renamed[$pos]

}
Get-foo -Renamed "Hello" -pos -1
