# Not same
$var = 10
0..10 | Select-Object @{n='SomeProperty';e={ $var = 30 * $_; $var }}

# Not same
$var = 10
Get-ChildItem | Rename-Item -NewName { $var = $_.FullName + (Get-Random); $var }

# Same
$var = 10
0..10 | ForEach-Object {
    $var += 5
}

# Not same
$var = 10
. (Get-Module Pester) { $var = 30 }

# Same
$var = 10
$sb = { $var = 30 }
. $sb

# ???
$var = 10
$sb = { $var = 30 }
$shouldDotSource = Get-Random -Minimum 0 -Maximum 2
if ($shouldDotSource) {
    . $sb
} else {
    & $sb
}
