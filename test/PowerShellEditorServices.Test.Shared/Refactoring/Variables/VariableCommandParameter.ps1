function Get-foo {
    param (
        [string]$string,
        [int]$pos
    )

    return $string[$pos]

}
Get-foo -string "Hello" -pos -1
