$i = 1

while ($i -le 500000)
{
    $str = "Output $i"
    Write-Host $str
    $i = $i + 1
}

Write-Host "Done!"
Get-Date
Get-Host