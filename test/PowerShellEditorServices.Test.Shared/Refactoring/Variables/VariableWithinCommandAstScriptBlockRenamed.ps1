# Not same
$var = 10
Get-ChildItem | Rename-Item -NewName { $Renamed = $_.FullName + (Get-Random); $Renamed }
