# Not same
$var = 10
Get-ChildItem | Rename-Item -NewName { $var = $_.FullName + (Get-Random); $var }
