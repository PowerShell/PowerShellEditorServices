param(
    [switch]
    $Clean
)

if ($Clean)
{
    $binDir = "$PSScriptRoot/bin"
    $objDir = "$PSScriptRoot/obj"
    foreach ($dir in $binDir,$objDir)
    {
        if (Test-Path $dir)
        {
            Remove-Item -Force -Recurse $dir
        }
    }
}

Push-Location $PSScriptRoot
try
{
    dotnet build
}
finally
{
    Pop-Location
}
