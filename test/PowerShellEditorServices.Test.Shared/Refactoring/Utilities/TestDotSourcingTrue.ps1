$sb = { $var = 30 }
$shouldDotSource = Get-Random -Minimum 0 -Maximum 2
if ($shouldDotSource) {
    . $sb
} else {
    & $sb
}
