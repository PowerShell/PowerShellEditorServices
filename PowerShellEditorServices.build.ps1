#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$PsesSubmodulePath = "$PSScriptRoot/module",

    [string]$ModulesJsonPath = "$PSScriptRoot/modules.json",

    [string]$DefaultModuleRepository = "PSGallery",

    [string]$TestFilter = ''
)

#Requires -Modules @{ModuleName="InvokeBuild";ModuleVersion="3.2.1"}

$script:IsUnix = $PSVersionTable.PSEdition -and $PSVersionTable.PSEdition -eq "Core" -and !$IsWindows
$script:TargetPlatform = "netstandard2.0"
$script:TargetFrameworksParam = "/p:TargetFrameworks=`"$script:TargetPlatform`""
$script:RequiredSdkVersion = (Get-Content (Join-Path $PSScriptRoot 'global.json') | ConvertFrom-Json).sdk.version
$script:NugetApiUriBase = 'https://www.nuget.org/api/v2/package'
$script:ModuleBinPath = "$PSScriptRoot/module/PowerShellEditorServices/bin/"
$script:VSCodeModuleBinPath = "$PSScriptRoot/module/PowerShellEditorServices.VSCode/bin/"
$script:WindowsPowerShellFrameworkTarget = 'net461'
$script:NetFrameworkPlatformId = 'win'
$script:BuildInfoPath = [System.IO.Path]::Combine($PSScriptRoot, "src", "PowerShellEditorServices.Host", "BuildInfo", "BuildInfo.cs")

$script:PSCoreModulePath = $null

$script:TestRuntime = @{
    'Core'    = 'netcoreapp2.1'
    'Desktop' = 'net461'
}

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
        'PowerShellEditorServices.Engine' = @(
            'publish/Microsoft.Extensions.DependencyInjection.Abstractions.dll',
            'publish/Microsoft.Extensions.DependencyInjection.dll',
            'publish/Microsoft.Extensions.FileSystemGlobbing.dll',
            'publish/Microsoft.Extensions.Logging.Abstractions.dll',
            'publish/Microsoft.Extensions.Logging.dll',
            'publish/Microsoft.Extensions.Options.dll',
            'publish/Microsoft.Extensions.Primitives.dll',
            'publish/Microsoft.PowerShell.EditorServices.Engine.dll',
            'publish/Microsoft.PowerShell.EditorServices.Engine.pdb',
            'publish/Newtonsoft.Json.dll',
            'publish/OmniSharp.Extensions.JsonRpc.dll',
            'publish/OmniSharp.Extensions.LanguageProtocol.dll',
            'publish/OmniSharp.Extensions.LanguageServer.dll',
            'publish/runtimes/linux-64/native/libdisablekeyecho.so',
            'publish/runtimes/osx-64/native/libdisablekeyecho.dylib',
            'publish/Serilog.dll',
            'publish/Serilog.Extensions.Logging.dll',
            'publish/Serilog.Sinks.File.dll',
            'publish/System.Reactive.dll',
            'publish/UnixConsoleEcho.dll'
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
    <Destination Bin Directory>: [{
        'PackageName': <Package Name>,
        'PackageVersion': <Package Version>,
        'TargetRuntime': <Target .NET Runtime>,
        'DllName'?: <Name of DLL to extract>
    }]
}
#>
$script:RequiredNugetBinaries = @{
    'Desktop' = @(
        @{ PackageName = 'System.Security.Principal.Windows'; PackageVersion = '4.5.0'; TargetRuntime = 'net461' },
        @{ PackageName = 'System.Security.AccessControl';     PackageVersion = '4.5.0'; TargetRuntime = 'net461' },
        @{ PackageName = 'System.IO.Pipes.AccessControl';     PackageVersion = '4.5.1'; TargetRuntime = 'net461' }
    )
}

if (Get-Command git -ErrorAction SilentlyContinue) {
    # ignore changes to this file
    git update-index --assume-unchanged "$PSScriptRoot/src/PowerShellEditorServices.Host/BuildInfo/BuildInfo.cs"
}

if ($PSVersionTable.PSEdition -ne "Core") {
    Add-Type -Assembly System.IO.Compression.FileSystem
}

function Restore-NugetAsmForRuntime {
    param(
        [ValidateNotNull()][string]$PackageName,
        [ValidateNotNull()][string]$PackageVersion,
        [string]$DllName,
        [string]$DestinationPath,
        [string]$TargetPlatform = $script:NetFrameworkPlatformId,
        [string]$TargetRuntime = $script:WindowsPowerShellFrameworkTarget
    )

    $tmpDir = Join-Path $PSScriptRoot '.tmp'
    if (-not (Test-Path $tmpDir)) {
        New-Item -ItemType Directory -Path $tmpDir
    }

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
        $guid = New-Guid
        $tmpNupkgPath = Join-Path $tmpDir "$guid.zip"
        if (Test-Path $tmpNupkgPath) {
            Remove-Item -Force $tmpNupkgPath
        }

        try {
            $packageUri = "$script:NugetApiUriBase/$PackageName/$PackageVersion"
            Invoke-WebRequest -Uri $packageUri -OutFile $tmpNupkgPath
            Expand-Archive -Path $tmpNupkgPath -DestinationPath $packageDirPath
        } finally {
            Remove-Item -Force $tmpNupkgPath -ErrorAction SilentlyContinue
        }
    }

    $internalPath = [System.IO.Path]::Combine($packageDirPath, 'runtimes', $TargetPlatform, 'lib', $TargetRuntime, $DllName)

    Copy-Item -Path $internalPath -Destination $DestinationPath -Force

    return $DestinationPath
}

function Invoke-WithCreateDefaultHook {
    param([scriptblock]$ScriptBlock)

    try
    {
        $env:PSES_TEST_USE_CREATE_DEFAULT = 1
        & $ScriptBlock
    } finally {
        Remove-Item env:PSES_TEST_USE_CREATE_DEFAULT
    }
}

task SetupDotNet -Before Clean, Build, TestHost, TestServer, TestProtocol, TestE2E, PackageNuGet {

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
        # dotnet --version can write to stderr, which causes builds to abort, therefore use --list-sdks instead.
        if ((& $dotnetExePath --list-sdks | ForEach-Object { $_.Split()[0] } ) -contains $script:RequiredSdkVersion) {
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
        Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/cli/v$script:RequiredSdkVersion/scripts/obtain/dotnet-install.$installScriptExt" -OutFile $installScriptPath
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
    exec { & $script:dotnetExe restore }
    exec { & $script:dotnetExe clean }
    Remove-Item $PSScriptRoot\.tmp -Recurse -Force -ErrorAction Ignore
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

    if ($env:TF_BUILD) {
        # SYSTEM_PHASENAME is the Job name.
        # Job names can only include `_` but that's not a valid character for versions.
        $jobname = $env:SYSTEM_PHASENAME -replace '_', ''
        $script:BuildNumber = "$jobname-$env:BUILD_BUILDID"
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

task CreateBuildInfo -Before Build {
    $buildVersion = "<development-build>"
    $buildOrigin = "<development>"

    # Set build info fields on build platforms
    if ($env:TF_BUILD)
    {
        $psd1Path = [System.IO.Path]::Combine($PSScriptRoot, "module", "PowerShellEditorServices", "PowerShellEditorServices.psd1")
        $buildVersion = (Import-PowerShellDataFile -LiteralPath $psd1Path).Version
        $buildOrigin = "VSTS"
    }

    # Allow override of build info fields (except date)
    if ($env:PSES_BUILD_VERSION)
    {
        $buildVersion = $env:PSES_BUILD_VERSION
    }

    if ($env:PSES_BUILD_ORIGIN)
    {
        $buildOrigin = $env:PSES_BUILD_ORIGIN
    }

    [string]$buildTime = [datetime]::Now.ToString("s", [System.Globalization.CultureInfo]::InvariantCulture)

    $buildInfoContents = @"
namespace Microsoft.PowerShell.EditorServices.Host
{
    public static class BuildInfo
    {
        public const string BuildVersion = "$buildVersion";
        public const string BuildOrigin = "$buildOrigin";
        public static readonly System.DateTime? BuildTime = System.DateTime.Parse("$buildTime");
    }
}
"@

    Set-Content -LiteralPath $script:BuildInfoPath -Value $buildInfoContents -Force
}

task Build {
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -f $script:TargetPlatform }
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices.Engine\PowerShellEditorServices.Engine.csproj -f $script:TargetPlatform }
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj -f $script:TargetPlatform }
    exec { & $script:dotnetExe build -c $Configuration .\src\PowerShellEditorServices.VSCode\PowerShellEditorServices.VSCode.csproj $script:TargetFrameworksParam }
}

function DotNetTestFilter {
    # Reference https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
    if ($TestFilter) { @("--filter",$TestFilter) } else { "" }
}

# task Test TestServer,TestProtocol,TestE2E
task Test TestE2E

task TestServer {
    Set-Location .\test\PowerShellEditorServices.Test\

    if (-not $script:IsUnix) {
        exec { & $script:dotnetExe test --logger trx -f $script:TestRuntime.Desktop (DotNetTestFilter) }
    }

    Invoke-WithCreateDefaultHook -NewModulePath $script:PSCoreModulePath {
        exec { & $script:dotnetExe test --logger trx -f $script:TestRuntime.Core (DotNetTestFilter) }
    }
}

task TestProtocol {
    Set-Location .\test\PowerShellEditorServices.Test.Protocol\

    if (-not $script:IsUnix) {
        exec { & $script:dotnetExe test --logger trx -f $script:TestRuntime.Desktop (DotNetTestFilter) }
    }

    Invoke-WithCreateDefaultHook {
        exec { & $script:dotnetExe test --logger trx -f $script:TestRuntime.Core (DotNetTestFilter) }
    }
}

task TestHost {
    Set-Location .\test\PowerShellEditorServices.Test.Host\

    if (-not $script:IsUnix) {
        exec { & $script:dotnetExe build -f $script:TestRuntime.Desktop }
        exec { & $script:dotnetExe test -f $script:TestRuntime.Desktop (DotNetTestFilter) }
    }

    exec { & $script:dotnetExe build -c $Configuration -f $script:TestRuntime.Core }
    exec { & $script:dotnetExe test -f $script:TestRuntime.Core (DotNetTestFilter) }
}

task TestE2E {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\

    $env:PWSH_EXE_NAME = if ($IsCoreCLR) { "pwsh" } else { "powershell" }
    exec { & $script:dotnetExe test --logger trx -f $script:TestRuntime.Core (DotNetTestFilter) }
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
            Restore-NugetAsmForRuntime -DestinationPath $binDestPath @packageDetails
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

    if ($moduleInfos.Keys.Count -gt 0) {
        # `#Requires` doesn't display the version needed in the error message and `using module` doesn't work with InvokeBuild in Windows PowerShell
        # so we'll just use Import-Module to check that PowerShellGet 1.6.0 or higher is installed.
        # This is needed in order to use the `-AllowPrerelease` parameter
        Import-Module -Name PowerShellGet -MinimumVersion 1.6.0 -ErrorAction Stop
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

task UploadArtifacts -If ($null -ne $env:TF_BUILD) {
    Copy-Item -Path .\PowerShellEditorServices-$($script:FullVersion).zip -Destination $env:BUILD_ARTIFACTSTAGINGDIRECTORY
}

# The default task is to run the entire CI build
task . GetProductVersion, Clean, Build, Test, BuildCmdletHelp, PackageNuGet, PackageModule, UploadArtifacts
