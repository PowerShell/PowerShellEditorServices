#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

#Requires -Modules @{ModuleName="InvokeBuild";ModuleVersion="3.2.1"}

$script:IsCIBuild = $env:APPVEYOR -ne $null
$script:IsUnix = $PSVersionTable.PSEdition -and $PSVersionTable.PSEdition -eq "Core" -and !$IsWindows
$script:TargetFrameworksParam = "/p:TargetFrameworks=\`"$(if (!$script:IsUnix) { "net451;" })netstandard1.6\`""

if ($PSVersionTable.PSEdition -ne "Core") {
    Add-Type -Assembly System.IO.Compression.FileSystem
}

task SetupDotNet -Before Restore, Clean, Build, BuildHost, Test, TestPowerShellApi, PackageNuGet {

    # Fetch the SDK version from global.json
    $globalJson = Get-Content $PSScriptRoot/global.json | ConvertFrom-Json
    $requiredSdkVersion = $globalJson.sdk.version

    # Alternative versions:
    # "version": "1.0.0-rc4-004598"
    # "version": "1.0.0-rc3-004517"
    # "version": "1.0.0-preview4-004233"

    $needsInstall = $true
    $dotnetPath = "$PSScriptRoot/.dotnet"
    $dotnetExePath = if ($script:IsUnix) { "$dotnetPath/dotnet" } else { "$dotnetPath/dotnet.exe" }
    $originalDotNetExePath = $dotnetExePath

    if (!(Test-Path $dotnetExePath)) {
        $installedDotnet = Get-Command dotnet -ErrorAction Ignore
        if ($installedDotnet) {
            $dotnetExePath = $installedDotnet.Source
        }
        else {
            $dotnetExePath = $null
        }
    }

    # Make sure the dotnet we found is the right version
    if ($dotnetExePath -and (& $dotnetExePath --version) -eq $requiredSdkVersion) {
        $script:dotnetExe = $dotnetExePath
    }
    else {
        # Clear the path so that we invoke installation
        $script:dotnetExe = $null
    }

    if ($script:dotnetExe -eq $null) {

        Write-Host "`n### Installing .NET CLI $requiredSdkVersion...`n" -ForegroundColor Green

        # The install script is platform-specific
        $installScriptExt = if ($script:IsUnix) { "sh" } else { "ps1" }

        # Download the official installation script and run it
        $installScriptPath = "$([System.IO.Path]::GetTempPath())dotnet-install.$installScriptExt"
        Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview4/scripts/obtain/dotnet-install.$installScriptExt" -OutFile $installScriptPath
        $env:DOTNET_INSTALL_DIR = "$PSScriptRoot/.dotnet"

        if (!$script:IsUnix) {
            & $installScriptPath -Version $requiredSdkVersion -InstallDir "$env:DOTNET_INSTALL_DIR"
        }
        else {
            & /bin/bash $installScriptPath -Version $requiredSdkVersion -InstallDir "$env:DOTNET_INSTALL_DIR"
            $env:PATH = $dotnetExeDir + [System.IO.Path]::PathSeparator + $env:PATH
        }

        Write-Host "`n### Installation complete." -ForegroundColor Green
        $script:dotnetExe = $originalDotnetExePath
    }

    # This variable is used internally by 'dotnet' to know where it's installed
    $script:dotnetExe = Resolve-Path $script:dotnetExe
    if (!$env:DOTNET_INSTALL_DIR)
    {
        $dotnetExeDir = [System.IO.Path]::GetDirectoryName($script:dotnetExe)
        $env:PATH = $dotnetExeDir + [System.IO.Path]::PathSeparator + $env:PATH
        $env:DOTNET_INSTALL_DIR = $dotnetExeDir
    }

    Write-Host "`n### Using dotnet v$requiredSDKVersion at path $script:dotnetExe`n" -ForegroundColor Green
}

function NeedsRestore($rootPath) {
    # This checks to see if the number of folders under a given
    # path (like "src" or "test") is greater than the number of
    # obj\project.assets.json files found under that path, implying
    # that those folders have not yet been restored.
    $projectAssets = (Get-ChildItem "$rootPath\*\obj\project.assets.json")
    return ($projectAssets -eq $null) -or ((Get-ChildItem $rootPath).Length -gt $projectAssests.Length)
}

task Restore -If { "Restore" -in $BuildTask -or (NeedsRestore(".\src")) -or (NeedsRestore(".\test")) } -Before Clean, Build, BuildHost, Test {
    exec { & $script:dotnetExe restore }
}

task Clean {
    exec { & $script:dotnetExe clean }
    Remove-Item .\module\PowerShellEditorServices\bin -Recurse -Force -ErrorAction Ignore
    Get-ChildItem -Recurse src\*.nupkg | Remove-Item -Force -ErrorAction Ignore
    Get-ChildItem .\module\PowerShellEditorServices\*.zip | Remove-Item -Force -ErrorAction Ignore
}

task GetProductVersion -Before PackageNuGet, PackageModule, UploadArtifacts {
    [xml]$props = Get-Content .\PowerShellEditorServices.Common.props

    $script:VersionSuffix = $props.Project.PropertyGroup.VersionSuffix
    $script:BaseVersion = "$($props.Project.PropertyGroup.VersionPrefix)-$($props.Project.PropertyGroup.VersionSuffix)"
    $script:FullVersion = "$($props.Project.PropertyGroup.VersionPrefix)-$($props.Project.PropertyGroup.VersionSuffix)"

    if ($env:APPVEYOR) {
        $script:BuildNumber = $env:APPVEYOR_BUILD_NUMBER
        $script:FullVersion = "$script:FullVersion-$script:BuildNumber"
        $script:VersionSuffix = "$script:VersionSuffix-$script:BuildNumber"
    }

    Write-Host "`n### Product Version: $script:FullVersion`n" -ForegroundColor Green
}

function BuildForPowerShellVersion($version) {
    # Restore packages for the specified version
    exec { & $script:dotnetExe restore .\src\PowerShellEditorServices\PowerShellEditorServices.csproj /p:PowerShellVersion=$version }

    Write-Host -ForegroundColor Green "`n### Testing API usage for PowerShell $version...`n"
    exec { & $script:dotnetExe build -f net451 .\src\PowerShellEditorServices\PowerShellEditorServices.csproj /p:PowerShellVersion=$version }
}

task TestPowerShellApi -If { !$script:IsUnix } {
    BuildForPowerShellVersion v3
    BuildForPowerShellVersion v4
    BuildForPowerShellVersion v5r1

    # Do a final restore to put everything back to normal
    exec { & $script:dotnetExe restore .\src\PowerShellEditorServices\PowerShellEditorServices.csproj }
}

task BuildHost {
    exec { & $script:dotnetExe build -c $Configuration .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj $script:TargetFrameworksParam }
}

task Build {
    exec { & $script:dotnetExe build -c $Configuration .\PowerShellEditorServices.sln $script:TargetFrameworksParam }
}

task Test -If { !$script:IsUnix } {
    exec { & $script:dotnetExe test -c $Configuration -f net451 .\test\PowerShellEditorServices.Test\PowerShellEditorServices.Test.csproj }
    exec { & $script:dotnetExe test -c $Configuration -f net451 .\test\PowerShellEditorServices.Test.Protocol\PowerShellEditorServices.Test.Protocol.csproj }
    exec { & $script:dotnetExe test -c $Configuration -f net451 .\test\PowerShellEditorServices.Test.Host\PowerShellEditorServices.Test.Host.csproj }
}

task LayoutModule -After Build, BuildHost {
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\ -Type Directory | Out-Null
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop -Type Directory | Out-Null
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\Core -Type Directory | Out-Null

    if (!$script:IsUnix) {
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\net451\* -Filter Microsoft.PowerShell.EditorServices*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\net451\Newtonsoft.Json.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
    }
    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6\* -Filter Microsoft.PowerShell.EditorServices*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\
}

task PackageNuGet {
    exec { & $script:dotnetExe pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices\PowerShellEditorServices.csproj $script:TargetFrameworksParam }
    exec { & $script:dotnetExe pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices.Protocol\PowerShellEditorServices.Protocol.csproj $script:TargetFrameworksParam }
    exec { & $script:dotnetExe pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj $script:TargetFrameworksParam }
}

task PackageModule {
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        "$PSScriptRoot/module/PowerShellEditorServices",
        "$PSScriptRoot/module/PowerShellEditorServices-$($script:FullVersion).zip",
        [System.IO.Compression.CompressionLevel]::Optimal,
        $true)
}

task UploadArtifacts -If ($script:IsCIBuild) {
    if ($env:APPVEYOR) {
        Push-AppveyorArtifact .\src\PowerShellEditorServices\bin\$Configuration\Microsoft.PowerShell.EditorServices.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\src\PowerShellEditorServices.Protocol\bin\$Configuration\Microsoft.PowerShell.EditorServices.Protocol.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\src\PowerShellEditorServices.Host\bin\$Configuration\Microsoft.PowerShell.EditorServices.Host.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\module\PowerShellEditorServices-$($script:FullVersion).zip
    }
}

task UploadTestLogs -After Test -If ($script:IsCIBuild) {
    $testLogsPath =  "$PSScriptRoot/test/PowerShellEditorServices.Test.Host/bin/$Configuration/net451/logs"
    $testLogsZipPath = "$PSScriptRoot/TestLogs.zip"

    if (Test-Path $testLogsPath) {
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $testLogsPath,
            $testLogsZipPath)

        Push-AppveyorArtifact $testLogsZipPath
    }
    else {
        Write-Host "`n### WARNING: Test logs could not be found!`n" -ForegroundColor Yellow
    }
}

# The default task is to run the entire CI build
task . GetProductVersion, Clean, Build, TestPowerShellApi, Test, PackageNuGet, PackageModule, UploadArtifacts
