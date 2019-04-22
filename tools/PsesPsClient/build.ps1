param(
    [switch]
    $Clean
)

if ($Clean)
{
    Remove-Item -Force -Recurse "$PSScriptRoot/bin","$PSScriptRoot/obj"
}

dotnet build
