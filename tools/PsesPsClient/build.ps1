#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [Parameter()]
    [string]
    $DotnetExe = 'dotnet',

    [switch]
    $Clean
)

$ErrorActionPreference = 'Stop'

$script:OutDir = "$PSScriptRoot/out"
$script:OutModDir = "$script:OutDir/PsesPsClient"

$script:ModuleComponents = @{
    "bin/Debug/netstandard2.0/publish/PsesPsClient.dll" = "PsesPsClient.dll"
    "bin/Debug/netstandard2.0/publish/Newtonsoft.Json.dll" = "Newtonsoft.Json.dll"
    "PsesPsClient.psm1" = "PsesPsClient.psm1"
    "PsesPsClient.psd1" = "PsesPsClient.psd1"
}

if ($Clean)
{
    $binDir = "$PSScriptRoot/bin"
    $objDir = "$PSScriptRoot/obj"
    foreach ($dir in $binDir,$objDir,$script:OutDir)
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
    & $DotnetExe publish --framework 'netstandard2.0'

    New-Item -Path $script:OutModDir -ItemType Directory
    foreach ($key in $script:ModuleComponents.get_Keys())
    {
        $val = $script:ModuleComponents[$key]
        Copy-Item -Path "$PSScriptRoot/$key" -Destination "$script:OutModDir/$val"
    }
}
finally
{
    Pop-Location
}
