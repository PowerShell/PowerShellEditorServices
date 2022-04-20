# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$PsesSubmodulePath = "$PSScriptRoot/module",

    [string]$ModulesJsonPath = "$PSScriptRoot/modules.json",

    [string]$DefaultModuleRepository = "PSGallery",

    # See: https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
    [string]$TestFilter = '',

    # See: https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test
    # E.g. use @("--logger", "console;verbosity=detailed") for detailed console output instead
    [string[]]$TestArgs = @("--logger", "trx")
)

#Requires -Modules @{ModuleName="InvokeBuild"; ModuleVersion="5.0.0"}
#Requires -Modules @{ModuleName="platyPS"; ModuleVersion="0.14.0"}

$script:dotnetTestArgs = @(
    "test"
    $TestArgs
    if ($TestFilter) { "--filter", $TestFilter }
    "--framework"
)

$script:IsNix = $IsLinux -or $IsMacOS
# For Apple M1, pwsh might be getting emulated, in which case we need to check
# for the proc_translated flag, otherwise we can check the architecture.
$script:IsAppleM1 = $IsMacOS -and ((sysctl -n sysctl.proc_translated) -eq 1 -or (uname -m) -eq "arm64")
$script:BuildInfoPath = [System.IO.Path]::Combine($PSScriptRoot, "src", "PowerShellEditorServices.Hosting", "BuildInfo.cs")
$script:PsesCommonProps = [xml](Get-Content -Raw "$PSScriptRoot/PowerShellEditorServices.Common.props")

$script:NetRuntime = @{
    PS7 = 'netcoreapp3.1'
    PS72 = 'net6.0'
    Desktop = 'net462'
    Standard = 'netstandard2.0'
}

$script:HostCoreOutput = "$PSScriptRoot/src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetRuntime.PS7)/publish"
$script:HostDeskOutput = "$PSScriptRoot/src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetRuntime.Desktop)/publish"
$script:PsesOutput = "$PSScriptRoot/src/PowerShellEditorServices/bin/$Configuration/$($script:NetRuntime.Standard)/publish"
$script:VSCodeOutput = "$PSScriptRoot/src/PowerShellEditorServices.VSCode/bin/$Configuration/$($script:NetRuntime.Standard)/publish"

if (Get-Command git -ErrorAction SilentlyContinue) {
    # ignore changes to this file
    git update-index --assume-unchanged "$PSScriptRoot/src/PowerShellEditorServices.Hosting/BuildInfo.cs"
}

task FindDotNet {
    assert (Get-Command dotnet -ErrorAction SilentlyContinue) "dotnet not found, please install it: https://aka.ms/dotnet-cli"

    # Strip out semantic version metadata so it can be cast to `Version`
    $existingVersion, $null = (dotnet --version) -split '-'
    assert ([Version]$existingVersion -ge [Version]("6.0")) ".NET SDK 6.0 or higher is required, please update it: https://aka.ms/dotnet-cli"

    # Anywhere other than on a Mac with an M1 processor, we additionally
    # need the .NET 3.1 runtime for our netcoreapp3.1 framework.
    if (!$script:IsAppleM1) {
        $runtimes = dotnet --list-runtimes
        assert ($runtimes -match "Microsoft.NETCore.App 3.1") ".NET Runtime 3.1 required but not found!"
    }

    Write-Host "Using dotnet v$(dotnet --version) at path $((Get-Command dotnet).Source)" -ForegroundColor Green
}

task BinClean {
    Remove-Item $PSScriptRoot\.tmp -Recurse -Force -ErrorAction Ignore
    Remove-Item $PSScriptRoot\module\PowerShellEditorServices\bin -Recurse -Force -ErrorAction Ignore
    Remove-Item $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin -Recurse -Force -ErrorAction Ignore
}

task Clean FindDotNet, BinClean, {
    exec { & dotnet clean }
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

task CreateBuildInfo {
    $buildVersion = "<development-build>"
    $buildOrigin = "Development"
    $buildCommit = git rev-parse HEAD

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

        if ($propsBody.VersionSuffix) {
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

    [string]$buildTime = [datetime]::Today.ToString("s", [System.Globalization.CultureInfo]::InvariantCulture)

    $buildInfoContents = @"
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace Microsoft.PowerShell.EditorServices.Hosting
{
    public static class BuildInfo
    {
        public static readonly string BuildVersion = "$buildVersion";
        public static readonly string BuildOrigin = "$buildOrigin";
        public static readonly string BuildCommit = "$buildCommit";
        public static readonly System.DateTime? BuildTime = System.DateTime.Parse("$buildTime", CultureInfo.InvariantCulture.DateTimeFormat);
    }
}
"@

    if (Compare-Object $buildInfoContents.Split([Environment]::NewLine) (Get-Content $script:BuildInfoPath)) {
        Write-Host "Updating Build Info"
        Set-Content -LiteralPath $script:BuildInfoPath -Value $buildInfoContents -Force
    }
}

task SetupHelpForTests {
    if (-not (Get-Help Write-Host).Examples) {
        Write-Host "Updating help for tests"
        Update-Help -Module Microsoft.PowerShell.Utility -Force -Scope CurrentUser
    }
}

Task Build FindDotNet, CreateBuildInfo, {
    exec { & dotnet restore }
    exec { & dotnet publish -c $Configuration .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -f $script:NetRuntime.Standard }
    exec { & dotnet publish -c $Configuration .\src\PowerShellEditorServices.Hosting\PowerShellEditorServices.Hosting.csproj -f $script:NetRuntime.PS7 }
    if (-not $script:IsNix) {
        exec { & dotnet publish -c $Configuration .\src\PowerShellEditorServices.Hosting\PowerShellEditorServices.Hosting.csproj -f $script:NetRuntime.Desktop }
    }

    # Build PowerShellEditorServices.VSCode module
    exec { & dotnet publish -c $Configuration .\src\PowerShellEditorServices.VSCode\PowerShellEditorServices.VSCode.csproj -f $script:NetRuntime.Standard }
}

task Test TestServer, TestE2E

task TestServer TestServerWinPS, TestServerPS7, TestServerPS72

Task TestServerWinPS -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    # TODO: See https://github.com/dotnet/sdk/issues/18353 for x64 test host
    # that is debuggable! If architecture is added, the assembly path gets an
    # additional folder, necesstiating fixes to find the commands definition
    # file and test files.
    exec { & dotnet $script:dotnetTestArgs $script:NetRuntime.Desktop }
}

task TestServerPS7 -If (-not $script:IsAppleM1) Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    exec { & dotnet $script:dotnetTestArgs $script:NetRuntime.PS7 }
}

task TestServerPS72 Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    exec { & dotnet $script:dotnetTestArgs $script:NetRuntime.PS72 }
}

task TestE2E Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\

    $env:PWSH_EXE_NAME = if ($IsCoreCLR) { "pwsh" } else { "powershell" }
    $NetRuntime = if ($IsAppleM1) { $script:NetRuntime.PS72 } else { $script:NetRuntime.PS7 }
    exec { & dotnet $script:dotnetTestArgs $NetRuntime }

    # Run E2E tests in ConstrainedLanguage mode.
    if (!$script:IsNix) {
        if (-not [Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown("BuiltInAdministratorsSid")) {
            Write-Warning 'Skipping E2E CLM tests as they must be ran in an elevated process.'
            return
        }

        try {
            [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", "0x80000007", [System.EnvironmentVariableTarget]::Machine);
            exec { & dotnet $script:dotnetTestArgs $script:NetRuntime.PS7 }
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

    foreach ($dir in $psesDepsPath,$psesCoreHostPath,$psesDeskHostPath,$psesVSCodeBinOutputPath) {
        New-Item -Force -Path $dir -ItemType Directory
    }

    # Copy Third Party Notices.txt to module folder
    Copy-Item -Force -Path "$PSScriptRoot\Third Party Notices.txt" -Destination $psesOutputPath

    # Assemble PSES module

    $includedDlls = [System.Collections.Generic.HashSet[string]]::new()
    [void]$includedDlls.Add('System.Management.Automation.dll')

    # PSES/bin/Common
    foreach ($psesComponent in Get-ChildItem $script:PsesOutput) {
        if ($psesComponent.Name -eq 'System.Management.Automation.dll' -or
            $psesComponent.Name -eq 'System.Runtime.InteropServices.RuntimeInformation.dll') {
            continue
        }

        if ($psesComponent.Extension) {
            [void]$includedDlls.Add($psesComponent.Name)
            Copy-Item -Path $psesComponent.FullName -Destination $psesDepsPath -Force
        }
    }

    # PSES/bin/Core
    foreach ($hostComponent in Get-ChildItem $script:HostCoreOutput) {
        if (-not $includedDlls.Contains($hostComponent.Name)) {
            Copy-Item -Path $hostComponent.FullName -Destination $psesCoreHostPath -Force
        }
    }

    # PSES/bin/Desktop
    if (-not $script:IsNix) {
        foreach ($hostComponent in Get-ChildItem $script:HostDeskOutput) {
            if (-not $includedDlls.Contains($hostComponent.Name)) {
                Copy-Item -Path $hostComponent.FullName -Destination $psesDeskHostPath -Force
            }
        }
    }

    # Assemble the PowerShellEditorServices.VSCode module

    foreach ($vscodeComponent in Get-ChildItem $script:VSCodeOutput) {
        if (-not $includedDlls.Contains($vscodeComponent.Name)) {
            Copy-Item -Path $vscodeComponent.FullName -Destination $psesVSCodeBinOutputPath -Force
        }
    }
}

task RestorePsesModules -After Build {
    $submodulePath = (Resolve-Path $PsesSubmodulePath).Path + [IO.Path]::DirectorySeparatorChar
    Write-Host "Restoring EditorServices modules..."

    # Read in the modules.json file as a hashtable so it can be splatted
    $moduleInfos = @{}

    (Get-Content -Raw $ModulesJsonPath | ConvertFrom-Json).PSObject.Properties | ForEach-Object {
        $name = $_.Name
        $body = @{
            Name = $name
            Version = $_.Value.Version
            AllowPrerelease = $_.Value.AllowPrerelease
            Repository = if ($_.Value.Repository) { $_.Value.Repository } else { $DefaultModuleRepository }
            Path = $submodulePath
        }

        if (-not $name) {
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
    foreach ($moduleName in $moduleInfos.Keys) {
        if (Test-Path -Path (Join-Path -Path $submodulePath -ChildPath $moduleName)) {
            Write-Host "`tModule '${moduleName}' already detected. Skipping"
            continue
        }

        $moduleInstallDetails = $moduleInfos[$moduleName]

        $splatParameters = @{
           Name = $moduleName
           RequiredVersion = $moduleInstallDetails.Version
           AllowPrerelease = $moduleInstallDetails.AllowPrerelease
           Repository = if ($moduleInstallDetails.Repository) { $moduleInstallDetails.Repository } else { $DefaultModuleRepository }
           Path = $submodulePath
        }

        Write-Host "`tInstalling module: ${moduleName} with arguments $(ConvertTo-Json $splatParameters)"

        Save-Module @splatParameters
    }
}

Task BuildCmdletHelp -After LayoutModule {
    New-ExternalHelp -Path $PSScriptRoot\module\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US -Force
    New-ExternalHelp -Path $PSScriptRoot\module\PowerShellEditorServices.VSCode\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices.VSCode\en-US -Force
}

# The default task is to run the entire CI build
task . Clean, Build, Test
