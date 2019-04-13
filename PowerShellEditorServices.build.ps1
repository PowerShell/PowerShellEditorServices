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
$script:TargetFrameworksParam = "/p:TargetFrameworks=\`"$(if (!$script:IsUnix) { "net452;" })netstandard1.6\`""
$script:SaveModuleSupportsAllowPrerelease = (Get-Command Save-Module).Parameters.ContainsKey("AllowPrerelease")
$script:BuildInfoPath = [System.IO.Path]::Combine($PSScriptRoot, "src", "PowerShellEditorServices.Host", "BuildInfo", "BuildInfo.cs")

if (Get-Command git -ErrorAction SilentlyContinue) {
    # ignore changes to this file
    git update-index --assume-unchanged "$PSScriptRoot/src/PowerShellEditorServices.Host/BuildInfo/BuildInfo.cs"
}

if ($PSVersionTable.PSEdition -ne "Core") {
    Add-Type -Assembly System.IO.Compression.FileSystem
}

task SetupDotNet -Before Clean, Build, TestHost, TestServer, TestProtocol, TestPowerShellApi {

    $requiredSdkVersion = "2.0.0"

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
        if ([version]$version -ge [version]$requiredSdkVersion) {
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

        Write-Host "`n### Installing .NET CLI $requiredSdkVersion...`n" -ForegroundColor Green

        # The install script is platform-specific
        $installScriptExt = if ($script:IsUnix) { "sh" } else { "ps1" }

        # Download the official installation script and run it
        $installScriptPath = "$([System.IO.Path]::GetTempPath())dotnet-install.$installScriptExt"
        Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/cli/v2.0.0/scripts/obtain/dotnet-install.$installScriptExt" -OutFile $installScriptPath
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

task GetProductVersion -Before PackageModule, UploadArtifacts {
    [xml]$props = Get-Content .\PowerShellEditorServices.Common.props

    $script:BuildNumber = 9999
    $script:VersionSuffix = $props.Project.PropertyGroup.VersionSuffix

    if ($env:TF_BUILD) {
        # SYSTEM_PHASENAME is the Job name.
        # Job names can only include `_` but that's not a valid character for versions.
        $jobname = $env:SYSTEM_PHASENAME -replace '_', ''
        $script:BuildNumber = "$jobname-$env:BUILD_BUILDNUMBER"
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

function BuildForPowerShellVersion($version) {
    Write-Host -ForegroundColor Green "`n### Testing API usage for PowerShell $version...`n"
    exec { & $script:dotnetExe build -f net452 .\src\PowerShellEditorServices\PowerShellEditorServices.csproj /p:PowerShellVersion=$version }
}

task TestPowerShellApi -If { !$script:IsUnix } {
    BuildForPowerShellVersion v3
    BuildForPowerShellVersion v4
    BuildForPowerShellVersion v5r1

    # Do a final restore to put everything back to normal
    exec { & $script:dotnetExe restore .\src\PowerShellEditorServices\PowerShellEditorServices.csproj }
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
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj -f netstandard1.6 }
    if (!$script:IsUnix) {
        exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj -f net452 }
    }
    exec { & $script:dotnetExe build -c $Configuration .\src\PowerShellEditorServices.VSCode\PowerShellEditorServices.VSCode.csproj $script:TargetFrameworksParam }
    exec { & $script:dotnetExe publish -c $Configuration .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -f netstandard1.6 }
    Copy-Item $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\netstandard1.6\publish\UnixConsoleEcho.dll -Destination $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6
    Copy-Item $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\netstandard1.6\publish\runtimes\osx-64\native\libdisablekeyecho.dylib -Destination $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6
    Copy-Item $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\netstandard1.6\publish\runtimes\linux-64\native\libdisablekeyecho.so -Destination $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6
}

function DotNetTestFilter {
    # Reference https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
    if ($TestFilter) { "-trait $TestFilter" } else { "" }
}

task Test TestServer,TestProtocol

task TestServer -If { !$script:IsUnix } {
    Set-Location .\test\PowerShellEditorServices.Test\
    exec { & $script:dotnetExe build -c $Configuration -f net452 }
    exec { & $script:dotnetExe xunit -configuration $Configuration -framework net452 -verbose -nobuild (XunitTraitFilter) }
}

task TestProtocol -If { !$script:IsUnix } {
    Set-Location .\test\PowerShellEditorServices.Test.Protocol\
    exec { & $script:dotnetExe build -c $Configuration -f net452 }
    exec { & $script:dotnetExe xunit -configuration $Configuration -framework net452 -verbose -nobuild (XunitTraitFilter) }
}

task TestHost -If { !$script:IsUnix } {
    Set-Location .\test\PowerShellEditorServices.Test.Host\
    exec { & $script:dotnetExe build -c $Configuration -f net452 }
    exec { & $script:dotnetExe xunit -configuration $Configuration -framework net452 -verbose -nobuild (XunitTraitFilter) }
}

task LayoutModule -After Build {
    # Lay out the PowerShellEditorServices module's binaries
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\ -Type Directory | Out-Null
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop -Type Directory | Out-Null
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\Core -Type Directory | Out-Null

    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\netstandard1.6\publish\Serilog*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\
    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\netstandard1.6\publish\System.Runtime.InteropServices.RuntimeInformation.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\

    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6\* -Filter Microsoft.PowerShell.EditorServices*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\
    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6\UnixConsoleEcho.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\
    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6\libdisablekeyecho.* -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\
    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6\publish\runtimes\win\lib\netstandard1.3\* -Filter System.IO.Pipes*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\

    if (!$script:IsUnix) {
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\net452\Serilog*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\net452\System.Runtime.InteropServices.RuntimeInformation.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\

        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\net452\* -Filter Microsoft.PowerShell.EditorServices*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\net452\Newtonsoft.Json.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\net452\UnixConsoleEcho.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
    }

    # Copy Third Party Notices.txt to module folder
    Copy-Item -Force -Path "$PSScriptRoot\Third Party Notices.txt" -Destination $PSScriptRoot\module\PowerShellEditorServices

    # Lay out the PowerShellEditorServices.VSCode module's binaries
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin\ -Type Directory | Out-Null
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin\Desktop -Type Directory | Out-Null
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin\Core -Type Directory | Out-Null

    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.VSCode\bin\$Configuration\netstandard1.6\* -Filter Microsoft.PowerShell.EditorServices.VSCode*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin\Core\
    if (!$script:IsUnix) {
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.VSCode\bin\$Configuration\net452\* -Filter Microsoft.PowerShell.EditorServices.VSCode*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin\Desktop\
    }

    if ($Configuration -eq "Debug") {
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.VSCode\bin\$Configuration\netstandard1.6\Microsoft.PowerShell.EditorServices.VSCode.pdb -Destination $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin\Core\
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\netstandard1.6\Microsoft.PowerShell.EditorServices.pdb -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6\Microsoft.PowerShell.EditorServices.Host.pdb -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\
        Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Protocol\bin\$Configuration\netstandard1.6\Microsoft.PowerShell.EditorServices.Protocol.pdb -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\

        if (!$script:IsUnix) {
            Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.VSCode\bin\$Configuration\net452\Microsoft.PowerShell.EditorServices.VSCode.pdb -Destination $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin\Desktop\
            Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices\bin\$Configuration\net452\Microsoft.PowerShell.EditorServices.pdb -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
            Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\net452\Microsoft.PowerShell.EditorServices.Host.pdb -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
            Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Protocol\bin\$Configuration\net452\Microsoft.PowerShell.EditorServices.Protocol.pdb -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
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
            Repository = if ($_.Value.Repository) { $_.Value.Repository } else { $DefaultModuleRepository }
            Path = $submodulePath
        }

        if (-not $name)
        {
            throw "EditorServices module listed without name in '$ModulesJsonPath'"
        }

        if ($script:SaveModuleSupportsAllowPrerelease)
        {
            $body += @{ AllowPrerelease = $_.Value.AllowPrerelease }
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
           Repository = if ($moduleInstallDetails.Repository) { $moduleInstallDetails.Repository } else { $DefaultModuleRepository }
           Path = $submodulePath
        }

        if ($script:SaveModuleSupportsAllowPrerelease)
        {
            $splatParameters += @{ AllowPrerelease = $moduleInstallDetails.AllowPrerelease }
        }

        Write-Host "`tInstalling module: ${moduleName}"

        Save-Module @splatParameters
    }
    Write-Host "`n"
}

task BuildCmdletHelp {
    New-ExternalHelp -Path $PSScriptRoot\module\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US -Force
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
task . GetProductVersion, Clean, Build, TestPowerShellApi, CITest, BuildCmdletHelp, PackageModule, UploadArtifacts
