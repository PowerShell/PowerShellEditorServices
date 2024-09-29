$oldVarName = 5
1..10 |
Where-Object { $_ -le $oldVarName } |
Write-Output
