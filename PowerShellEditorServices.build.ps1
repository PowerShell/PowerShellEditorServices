#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$PsesSubmodulePath = "$PSScriptRoot/module",

    [string]$ModulesJsonPath = "$PSScriptRoot/modules.json",

    [string]$DefaultModuleRepository = "PSGallery"
)

#Requires -Modules @{ModuleName="InvokeBuild";ModuleVersion="3.2.1"}

$script:IsCIBuild = $env:APPVEYOR -ne $null
$script:IsUnix = $PSVersionTable.PSEdition -and $PSVersionTable.PSEdition -eq "Core" -and !$IsWindows
$script:TargetPlatform = "netstandard2.0"
$script:TargetFrameworksParam = "/p:TargetFrameworks=`"$script:TargetPlatform`""
$script:SaveModuleSupportsAllowPrerelease = (Get-Command Save-Module).Parameters.ContainsKey("AllowPrerelease")
$script:RequiredSdkVersion = "2.1.301"
$script:NugetApiUriBase = 'https://www.nuget.org/api/v2/package'
$script:ModuleBinPath = "$PSScriptRoot/module/PowerShellEditorServices/bin/"
$script:VSCodeModuleBinPath = "$PSScriptRoot/module/PowerShellEditorServices.VSCode/bin/"
$script:WindowsPowerShellFrameworkTarget = 'net461'
$script:NetFrameworkPlatformId = 'win'

<#
Declarative specification of binary assets produced
in the build that need to be binplaced in the module.
Schema is:
{
    <Output Path>: {
        <Project Name>: [
            <FilePath From Project Build Folder>
        ]
    }
}
#>
$script:RequiredBuildAssets = @{
    $script:ModuleBinPath = @{
        'PowerShellEditorServices' = @(
            'publish/Serilog.dll',
            'publish/Serilog.Sinks.Async.dll',
            'publish/Serilog.Sinks.Console.dll',
            'publish/Serilog.Sinks.File.dll',
            'Microsoft.PowerShell.EditorServices.dll',
            'Microsoft.PowerShell.EditorServices.pdb'
        )

        'PowerShellEditorServices.Host' = @(
            'publish/UnixConsoleEcho.dll',
            'publish/runtimes/osx-64/native/libdisablekeyecho.dylib',
            'publish/runtimes/linux-64/native/libdisablekeyecho.so',
            'publish/Newtonsoft.Json.dll',
            'Microsoft.PowerShell.EditorServices.Host.dll',
            'Microsoft.PowerShell.EditorServices.Host.pdb'
        )

        'PowerShellEditorServices.Protocol' = @(
            'Microsoft.PowerShell.EditorServices.Protocol.dll',
            'Microsoft.PowerShell.EditorServices.Protocol.pdb'
        )
    }

    $script:VSCodeModuleBinPath = @{
        'PowerShellEditorServices.VSCode' = @(
            'Microsoft.PowerShell.EditorServices.VSCode.dll',
            'Microsoft.PowerShell.EditorServices.VSCode.pdb'
        )
    }
}

<#
Declares the binary shims we need to make the netstandard DLLs hook into .NET Framework.
Schema is:
{
    <Destination Bin Directory>: {
        'TargetRuntime': <Target .NET Runtime>,
        'Packages': [{
            'PackageName': <Package Name>,
            'PackageVersion': <Package Version>,
            'DllName'?: <Name of DLL to extract>
        }]
    }
}
#>
$script:RequiredNugetBinaries = @{
    'Desktop' = @(
        @{ PackageName = 'System.Security.Principal.Windows'; PackageVersion = '4.5.0'; TargetRuntime = 'net461' },
        @{ PackageName = 'System.Security.AccessControl';     PackageVersion = '4.5.0'; TargetRuntime = 'net461' },
        @{ PackageName = 'System.IO.Pipes.AccessControl';     PackageVersion = '4.5.1'; TargetRuntime = 'net461' }
    )

    '6.0' = @(
        @{ PackageName = 'System.Security.Principal.Windows'; PackageVersion = '4.5.0'; TargetRuntime = 'netcoreapp2.0' },
        @{ PackageName = 'System.Security.AccessControl';     PackageVersion = '4.5.0'; TargetRuntime = 'netcoreapp2.0' },
        @{ PackageName = 'System.IO.Pipes.AccessControl';     PackageVersion = '4.5.1'; TargetRuntime = 'netstandard2.0' }
    )
}

if ($PSVersionTable.PSEdition -ne "Core") {
    Add-Type -Assembly System.IO.Compression.FileSystem
}

function Get-NugetAsmForRuntime {
    param(
        [ValidateNotNull()][string]$PackageName,
        [ValidateNotNull()][string]$PackageVersion,
        [string]$DllName,
        [string]$DestinationPath,
        [string]$TargetPlatform = $script:NetFrameworkPlatformId,
        [string]$TargetRuntime = $script:WindowsPowerShellFrameworkTarget
    )

    $tmpDir = [System.IO.Path]::GetTempPath()

    if (-not $DllName) {
        $DllName = "$PackageName.dll"
    }

    if ($DestinationPath -eq $null) {
        $DestinationPath = Join-Path $tmpDir $DllName
    } elseif (Test-Path $DestinationPath -PathType Container) {
        $DestinationPath = Join-Path $DestinationPath $DllName
    }

    $packageDirPath = Join-Path $tmpDir "$PackageName.$PackageVersion"
    if (-not (Test-Path $packageDirPath)) {
        $tmpNupkgPath = Join-Path $tmpDir 'tmp.zip'
        if (Test-Path $tmpNupkgPath) {
            Remove-Item -Force $tmpNupkgPath
        }

        $packageUri = "$script:NugetApiUriBase/$PackageName/$PackageVersion"
        Invoke-WebRequest -Uri $packageUri -OutFile $tmpNupkgPath
        Expand-Archive -Path $tmpNupkgPath -DestinationPath $packageDirPath
    }

    $internalPath = [System.IO.Path]::Combine($packageDirPath, 'runtimes', $TargetPlatform, 'lib', $TargetRuntime, $DllName)

    Copy-Item -Path $internalPath -Destination $DestinationPath -Force

    return $DestinationPath
}

task SetupDotNet -Before Clean, Build, TestHost, TestServer, TestProtocol, PackageNuGet {

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
    if ($dotnetExePath) {
        # dotnet --version can return a semver that System.Version can't handle
        # e.g.: 2.1.300-preview-01. The replace operator is used to remove any build suffix.
        $version = (& $dotnetExePath --version) -replace '[+-].*$',''
        if ([version]$version -ge [version]$script:RequiredSdkVersion) {
            $script:dotnetExe = $dotnetExePath
        }
        else {
            # Clear the path so that we invoke installation
            $script:dotnetExe = $null
        }
    }
    else {
        # Clear the path so that we invoke installation
        $script:dotnetExe = $null
    }

    if ($script:dotnetExe -eq $null) {

        Write-Host "`n### Installing .NET CLI $script:RequiredSdkVersion...`n" -ForegroundColor Green

        # The install script is platform-specific
        $installScriptExt = if ($script:IsUnix) { "sh" } else { "ps1" }

        # Download the official installation script and run it
        $installScriptPath = "$([System.IO.Path]::GetTempPath())dotnet-install.$installScriptExt"
        Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/cli/v2.0.0/scripts/obtain/dotnet-install.$installScriptExt" -OutFile $installScriptPath
        $env:DOTNET_INSTALL_DIR = "$PSScriptRoot/.dotnet"

        if (!$script:IsUnix) {
            & $installScriptPath -Version $script:RequiredSdkVersion -InstallDir "$env:DOTNET_INSTALL_DIR"
        }
        else {
            & /bin/bash $installScriptPath -Version $script:RequiredSdkVersion -InstallDir "$env:DOTNET_INSTALL_DIR"
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

    Write-Host "`n### Using dotnet v$(& $script:dotnetExe --version) at path $script:dotnetExe`n" -ForegroundColor Green
}

task Clean {
    exec { & $script:dotnetExe clean }
    Remove-Item $PSScriptRoot\module\PowerShellEditorServices\bin -Recurse -Force -ErrorAction Ignore
    Remove-Item $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin -Recurse -Force -ErrorAction Ignore
    Get-ChildItem -Recurse $PSScriptRoot\src\*.nupkg | Remove-Item -Force -ErrorAction Ignore
    Get-ChildItem $PSScriptRoot\PowerShellEditorServices*.zip | Remove-Item -Force -ErrorAction Ignore
    Get-ChildItem $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US\*-help.xml | Remove-Item -Force -ErrorAction Ignore
}

task GetProductVersion -Before PackageNuGet, PackageModule, UploadArtifacts {
    [xml]$props = Get-Content .\PowerShellEditorServices.Common.props

    $script:BuildNumber = 9999
    $script:VersionSuffix = $props.Project.PropertyGroup.VersionSuffix

    if ($env:APPVEYOR) {
        $script:BuildNumber = $env:APPVEYOR_BUILD_NUMBER
    }

    if ($script:VersionSuffix -ne $null) {
        $script:VersionSuffix = "$script:VersionSuffix-$script:BuildNumber"
    }
    else {
        $script:VersionSuffix = "$script:BuildNumber"
    }

    $script:FullVersion = "$($props.Project.PropertyGroup.VersionPrefix)-$script:VersionSuffix"

    Write-Host "`n### Product Version: $script:FullVersion`n" -ForegroundColor Green
}

task Build {
    exec { & $script:dotnetExe restore }
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj -f $script:TargetPlatform }
    exec { & $script:dotnetExe build -c $Configuration .\src\PowerShellEditorServices.VSCode\PowerShellEditorServices.VSCode.csproj $script:TargetFrameworksParam }
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -f $script:TargetPlatform }
}

function UploadTestLogs {
    if ($script:IsCIBuild) {
        $testLogsPath =  "$PSScriptRoot/test/PowerShellEditorServices.Test.Host/bin/$Configuration/net452/logs"
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
}

task Test TestServer,TestProtocol

task TestServer {
    Set-Location .\test\PowerShellEditorServices.Test\

    if (-not $script:IsUnix) {
        exec { & $script:dotnetExe build -f net461 }
        exec { & $script:dotnetExe test -f net461 }
    }

    exec { & $script:dotnetExe build -c $Configuration -f netcoreapp2.1 }
    exec { & $script:dotnetExe test -f netcoreapp2.1 }
}

task TestProtocol {
    Set-Location .\test\PowerShellEditorServices.Test.Protocol\

    if (-not $script:IsUnix) {
        exec { & $script:dotnetExe build -f net461 }
        exec { & $script:dotnetExe test -f net461 }
    }

    exec { & $script:dotnetExe build -c $Configuration -f netcoreapp2.1 }
    exec { & $script:dotnetExe test -f netcoreapp2.1 }
}

task TestHost -If {
    Set-Location .\test\PowerShellEditorServices.Test.Host\

    if (-not $script:IsUnix) {
        exec { & $script:dotnetExe build -f net461 }
        exec { & $script:dotnetExe test -f net461 }
    }

    exec { & $script:dotnetExe build -c $Configuration -f netcoreapp2.1 }
    exec { & $script:dotnetExe test -f netcoreapp2.1 }
}

task CITest ?Test, {
    # This task is used to ensure we have a chance to upload
    # test logs as a CI artifact when the tests fail
    if (error Test) {
        UploadTestLogs
        Write-Error "Failing build due to test failure."
    }
}

task LayoutModule -After Build {
    # Copy Third Party Notices.txt to module folder
    Copy-Item -Force -Path "$PSScriptRoot\Third Party Notices.txt" -Destination $PSScriptRoot\module\PowerShellEditorServices

    # Lay out the PowerShellEditorServices module's binaries
    # For each binplace destination
    foreach ($destDir in $script:RequiredBuildAssets.Keys) {
        # Create the destination dir
        $null = New-Item -Force $destDir -Type Directory

        # For each PSES subproject
        foreach ($projectName in $script:RequiredBuildAssets[$destDir].Keys) {
            # Get the project build dir path
            $basePath = [System.IO.Path]::Combine($PSScriptRoot, 'src', $projectName, 'bin', $Configuration, $script:TargetPlatform)

            # For each asset in the subproject
            foreach ($bin in $script:RequiredBuildAssets[$destDir][$projectName]) {
                # Get the asset path
                $binPath = Join-Path $basePath $bin

                # Binplace the asset
                Copy-Item -Force -Verbose $binPath $destDir
            }
        }
    }

    # Get and place the shim bins for net461
    foreach ($binDestinationDir in $script:RequiredNugetBinaries.Keys) {
        $binDestPath = Join-Path $script:ModuleBinPath $binDestinationDir
        if (-not (Test-Path $binDestPath)) {
            New-Item -Path $binDestPath -ItemType Directory
        }

        foreach ($packageDetails in $script:RequiredNugetBinaries[$binDestinationDir]) {
            Get-NugetAsmForRuntime -DestinationPath $binDestPath @packageDetails
        }
    }
}

task RestorePsesModules -After Build {
    $submodulePath = (Resolve-Path $PsesSubmodulePath).Path + [IO.Path]::DirectorySeparatorChar
    Write-Host "`nRestoring EditorServices modules..."

    # Read in the modules.json file as a hashtable so it can be splatted
    $moduleInfos = @{}

    (Get-Content -Raw $ModulesJsonPath | ConvertFrom-Json).PSObject.Properties | ForEach-Object {
        $name = $_.Name
        $body = @{
            Name = $name
            MinimumVersion = $_.Value.MinimumVersion
            MaximumVersion = $_.Value.MaximumVersion
            AllowPrerelease = $_.Value.AllowPrerelease
            Repository = if ($_.Value.Repository) { $_.Value.Repository } else { $DefaultModuleRepository }
            Path = $submodulePath
        }

        if (-not $name)
        {
            throw "EditorServices module listed without name in '$ModulesJsonPath'"
        }

        $moduleInfos.Add($name, $body)
    }

    # Save each module in the modules.json file
    foreach ($moduleName in $moduleInfos.Keys)
    {
        if (Test-Path -Path (Join-Path -Path $submodulePath -ChildPath $moduleName))
        {
            Write-Host "`tModule '${moduleName}' already detected. Skipping"
            continue
        }

        $moduleInstallDetails = $moduleInfos[$moduleName]

        $splatParameters = @{
           Name = $moduleName
           MinimumVersion = $moduleInstallDetails.MinimumVersion
           MaximumVersion = $moduleInstallDetails.MaximumVersion
           AllowPrerelease = $moduleInstallDetails.AllowPrerelease
           Repository = if ($moduleInstallDetails.Repository) { $moduleInstallDetails.Repository } else { $DefaultModuleRepository }
           Path = $submodulePath
        }

        Write-Host "`tInstalling module: ${moduleName} with arguments $(ConvertTo-Json $splatParameters)"

        Save-Module @splatParameters
    }

    Write-Host "`n"
}

task BuildCmdletHelp {
    New-ExternalHelp -Path $PSScriptRoot\module\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US -Force
}

task PackageNuGet {
    exec { & $script:dotnetExe pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices\PowerShellEditorServices.csproj $script:TargetFrameworksParam }
    exec { & $script:dotnetExe pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices.Protocol\PowerShellEditorServices.Protocol.csproj $script:TargetFrameworksParam }
    exec { & $script:dotnetExe pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj $script:TargetFrameworksParam }
}

task PackageModule {
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        "$PSScriptRoot/module/",
        "$PSScriptRoot/PowerShellEditorServices-$($script:FullVersion).zip",
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
}

task UploadArtifacts -If ($script:IsCIBuild) {
    if ($env:APPVEYOR) {
        Push-AppveyorArtifact .\src\PowerShellEditorServices\bin\$Configuration\Microsoft.PowerShell.EditorServices.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\src\PowerShellEditorServices.Protocol\bin\$Configuration\Microsoft.PowerShell.EditorServices.Protocol.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\src\PowerShellEditorServices.Host\bin\$Configuration\Microsoft.PowerShell.EditorServices.Host.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\PowerShellEditorServices-$($script:FullVersion).zip
    }
}

# The default task is to run the entire CI build
task . GetProductVersion, Clean, Build, CITest, BuildCmdletHelp, PackageNuGet, PackageModule, UploadArtifacts
