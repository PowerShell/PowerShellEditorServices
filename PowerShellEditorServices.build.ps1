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
$script:RequiredSdkVersion = (Get-Content (Join-Path $PSScriptRoot 'global.json') | ConvertFrom-Json).sdk.version
$script:BuildInfoPath = [System.IO.Path]::Combine($PSScriptRoot, "src", "PowerShellEditorServices.Hosting", "BuildInfo.cs")
$script:PsesCommonProps = [xml](Get-Content -Raw "$PSScriptRoot/PowerShellEditorServices.Common.props")
$script:IsPreview = [bool]($script:PsesCommonProps.Project.PropertyGroup.VersionSuffix)

$script:NetRuntime = @{
    Core = 'netcoreapp2.1'
    Desktop = 'net461'
    Standard = 'netstandard2.0'
}

$script:HostCoreOutput = "$PSScriptRoot/src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetRuntime.Core)/publish"
$script:HostDeskOutput = "$PSScriptRoot/src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetRuntime.Desktop)/publish"
$script:PsesOutput = "$PSScriptRoot/src/PowerShellEditorServices/bin/$Configuration/$($script:NetRuntime.Standard)/publish"
$script:VSCodeOutput = "$PSScriptRoot/src/PowerShellEditorServices.VSCode/bin/$Configuration/$($script:NetRuntime.Standard)/publish"

if (Get-Command git -ErrorAction SilentlyContinue) {
    # ignore changes to this file
    git update-index --assume-unchanged "$PSScriptRoot/src/PowerShellEditorServices.Hosting/BuildInfo.cs"
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

task SetupDotNet -Before Clean, Build, TestHost, TestServer, TestE2E {

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
        Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/sdk/master/scripts/obtain/dotnet-install.$installScriptExt" -OutFile $installScriptPath
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

task BinClean {
    Remove-Item $PSScriptRoot\.tmp -Recurse -Force -ErrorAction Ignore
    Remove-Item $PSScriptRoot\module\PowerShellEditorServices\bin -Recurse -Force -ErrorAction Ignore
    Remove-Item $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin -Recurse -Force -ErrorAction Ignore
}

task Clean BinClean,{
    exec { & $script:dotnetExe restore }
    exec { & $script:dotnetExe clean }
    Get-ChildItem -Recurse $PSScriptRoot\src\*.nupkg | Remove-Item -Force -ErrorAction Ignore
    Get-ChildItem $PSScriptRoot\PowerShellEditorServices*.zip | Remove-Item -Force -ErrorAction Ignore
    Get-ChildItem $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US\*-help.xml | Remove-Item -Force -ErrorAction Ignore

    # Remove bundled component modules
    $moduleJsonPath = "$PSScriptRoot\modules.json"
    if (Test-Path $moduleJsonPath) {
        Get-Content -Raw $moduleJsonPath |
            ConvertFrom-Json |
            ForEach-Object { $_.PSObject.Properties.Name } |
            ForEach-Object { Remove-Item -Path "$PSScriptRoot/module/$_" -Recurse -Force -ErrorAction Ignore }
    }
}

task GetProductVersion -Before PackageModule, UploadArtifacts {
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
    $buildOrigin = "Development"

    # Set build info fields on build platforms
    if ($env:TF_BUILD) {
        if ($env:BUILD_BUILDNUMBER -like "PR-*") {
            $buildOrigin = "PR"
        } elseif ($env:BUILD_DEFINITIONNAME -like "*-CI") {
            $buildOrigin = "CI"
        } else {
            $buildOrigin = "Release"
        }

        $propsXml = [xml](Get-Content -Raw -LiteralPath "$PSScriptRoot/PowerShellEditorServices.Common.props")
        $propsBody = $propsXml.Project.PropertyGroup
        $buildVersion = $propsBody.VersionPrefix

        if ($propsBody.VersionSuffix)
        {
            $buildVersion += '-' + $propsBody.VersionSuffix
        }
    }

    # Allow override of build info fields (except date)
    if ($env:PSES_BUILD_VERSION) {
        $buildVersion = $env:PSES_BUILD_VERSION
    }

    if ($env:PSES_BUILD_ORIGIN) {
        $buildOrigin = $env:PSES_BUILD_ORIGIN
    }

    [string]$buildTime = [datetime]::Now.ToString("s", [System.Globalization.CultureInfo]::InvariantCulture)

    $buildInfoContents = @"
using System.Globalization;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public static class BuildInfo
    {
        public static readonly string BuildVersion = "$buildVersion";
        public static readonly string BuildOrigin = "$buildOrigin";
        public static readonly System.DateTime? BuildTime = System.DateTime.Parse("$buildTime", CultureInfo.InvariantCulture.DateTimeFormat);
    }
}
"@

    Set-Content -LiteralPath $script:BuildInfoPath -Value $buildInfoContents -Force
}

task SetupHelpForTests -Before Test {
    if (-not (Get-Help Write-Host).Examples) {
        Update-Help -Module Microsoft.PowerShell.Utility -Force -Scope CurrentUser
    }
}

task Build BinClean,{
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -f $script:NetRuntime.Standard }
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices.Hosting\PowerShellEditorServices.Hosting.csproj -f $script:NetRuntime.Core }
    if (-not $script:IsUnix)
    {
        exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices.Hosting\PowerShellEditorServices.Hosting.csproj -f $script:NetRuntime.Desktop }
    }

    # Build PowerShellEditorServices.VSCode module
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices.VSCode\PowerShellEditorServices.VSCode.csproj -f $script:NetRuntime.Standard }
}

function DotNetTestFilter {
    # Reference https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
    if ($TestFilter) { @("--filter",$TestFilter) } else { "" }
}

task Test TestServer,TestE2E

task TestServer {
    Set-Location .\test\PowerShellEditorServices.Test\

    if (-not $script:IsUnix) {
        exec { & $script:dotnetExe test --logger trx -f $script:NetRuntime.Desktop (DotNetTestFilter) }
    }

    Invoke-WithCreateDefaultHook -NewModulePath $script:PSCoreModulePath {
        exec { & $script:dotnetExe test --logger trx -f $script:NetRuntime.Core (DotNetTestFilter) }
    }
}

task TestHost {
    Set-Location .\test\PowerShellEditorServices.Test.Host\

    if (-not $script:IsUnix) {
        exec { & $script:dotnetExe build -f $script:NetRuntime.Desktop }
        exec { & $script:dotnetExe test -f $script:NetRuntime.Desktop (DotNetTestFilter) }
    }

    exec { & $script:dotnetExe build -c $Configuration -f $script:NetRuntime.Core }
    exec { & $script:dotnetExe test -f $script:NetRuntime.Core (DotNetTestFilter) }
}

task TestE2E {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\

    $env:PWSH_EXE_NAME = if ($IsCoreCLR) { "pwsh" } else { "powershell" }
    exec { & $script:dotnetExe test --logger trx -f $script:NetRuntime.Core (DotNetTestFilter) }

    # Run E2E tests in ConstrainedLanguage mode.
    if (!$script:IsUnix) {
        try {
            [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", "0x80000007", [System.EnvironmentVariableTarget]::Machine);
            exec { & $script:dotnetExe test --logger trx -f $script:NetRuntime.Core (DotNetTestFilter) }
        } finally {
            [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", $null, [System.EnvironmentVariableTarget]::Machine);
        }
    }
}

task LayoutModule -After Build {
    $modulesDir = "$PSScriptRoot/module"
    $psesVSCodeBinOutputPath = "$modulesDir/PowerShellEditorServices.VSCode/bin"
    $psesOutputPath = "$modulesDir/PowerShellEditorServices"
    $psesBinOutputPath = "$PSScriptRoot/module/PowerShellEditorServices/bin"
    $psesDepsPath = "$psesBinOutputPath/Common"
    $psesCoreHostPath = "$psesBinOutputPath/Core"
    $psesDeskHostPath = "$psesBinOutputPath/Desktop"

    foreach ($dir in $psesDepsPath,$psesCoreHostPath,$psesDeskHostPath,$psesVSCodeBinOutputPath)
    {
        New-Item -Force -Path $dir -ItemType Directory
    }

    # Copy Third Party Notices.txt to module folder
    Copy-Item -Force -Path "$PSScriptRoot\Third Party Notices.txt" -Destination $psesOutputPath

    # Copy UnixConsoleEcho native libraries
    Copy-Item -Path "$script:PsesOutput/runtimes/osx-64/native/*" -Destination $psesDepsPath -Force
    Copy-Item -Path "$script:PsesOutput/runtimes/linux-64/native/*" -Destination $psesDepsPath -Force

    # Assemble PSES module

    $includedDlls = [System.Collections.Generic.HashSet[string]]::new()
    [void]$includedDlls.Add('System.Management.Automation.dll')

    # PSES/bin/Common
    foreach ($psesComponent in Get-ChildItem $script:PsesOutput)
    {
        if ($psesComponent.Name -eq 'System.Management.Automation.dll' -or
            $psesComponent.Name -eq 'System.Runtime.InteropServices.RuntimeInformation.dll')
        {
            continue
        }

        if ($psesComponent.Extension)
        {
            [void]$includedDlls.Add($psesComponent.Name)
            Copy-Item -Path $psesComponent.FullName -Destination $psesDepsPath -Force
        }
    }

    # PSES/bin/Core
    foreach ($hostComponent in Get-ChildItem $script:HostCoreOutput)
    {
        if (-not $includedDlls.Contains($hostComponent.Name))
        {
            Copy-Item -Path $hostComponent.FullName -Destination $psesCoreHostPath -Force
        }
    }

    # PSES/bin/Desktop
    if (-not $script:IsUnix)
    {
        foreach ($hostComponent in Get-ChildItem $script:HostDeskOutput)
        {
            if (-not $includedDlls.Contains($hostComponent.Name))
            {
                Copy-Item -Path $hostComponent.FullName -Destination $psesDeskHostPath -Force
            }
        }
    }

    # Assemble the PowerShellEditorServices.VSCode module

    foreach ($vscodeComponent in Get-ChildItem $script:VSCodeOutput)
    {
        if (-not $includedDlls.Contains($vscodeComponent.Name))
        {
            Copy-Item -Path $vscodeComponent.FullName -Destination $psesVSCodeBinOutputPath -Force
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
            AllowPrerelease = $script:IsPreview
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
           AllowPrerelease = $moduleInstallDetails.AllowPrerelease
           Repository = if ($moduleInstallDetails.Repository) { $moduleInstallDetails.Repository } else { $DefaultModuleRepository }
           Path = $submodulePath
        }

        # Only add Min and Max version if we're doing a stable release.
        # This is due to a PSGet issue with AllowPrerelease not installing the latest preview.
        if (!$moduleInstallDetails.AllowPrerelease) {
            $splatParameters.MinimumVersion = $moduleInstallDetails.MinimumVersion
            $splatParameters.MaximumVersion = $moduleInstallDetails.MaximumVersion
        }

        Write-Host "`tInstalling module: ${moduleName} with arguments $(ConvertTo-Json $splatParameters)"

        Save-Module @splatParameters
    }

    Write-Host "`n"
}

task BuildCmdletHelp {
    New-ExternalHelp -Path $PSScriptRoot\module\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US -Force
    New-ExternalHelp -Path $PSScriptRoot\module\PowerShellEditorServices.VSCode\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices.VSCode\en-US -Force
}

task PackageModule {
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        "$PSScriptRoot/module/",
        "$PSScriptRoot/PowerShellEditorServices-$($script:FullVersion).zip",
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)
}

task UploadArtifacts -If ($null -ne $env:TF_BUILD) {
    Copy-Item -Path .\PowerShellEditorServices-$($script:FullVersion).zip -Destination $env:BUILD_ARTIFACTSTAGINGDIRECTORY -Force
}

# The default task is to run the entire CI build
task . GetProductVersion, Clean, Build, Test, BuildCmdletHelp, PackageModule, UploadArtifacts
