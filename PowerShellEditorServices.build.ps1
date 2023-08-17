# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$LocalOmniSharp,

    [string]$PsesSubmodulePath = "$PSScriptRoot/module",

    [string]$ModulesJsonPath = "$PSScriptRoot/modules.json",

    [string]$DefaultModuleRepository = "PSGallery",

    [string]$Verbosity = "quiet",

    # See: https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
    [string]$TestFilter = '',

    # See: https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test
    [string[]]$TestArgs = @("--logger", "console;verbosity=normal", "--logger", "trx")
)

#Requires -Modules @{ModuleName="InvokeBuild"; ModuleVersion="5.0.0"}
#Requires -Modules @{ModuleName="platyPS"; ModuleVersion="0.14.0"}

$script:dotnetBuildArgs = @(
    "--verbosity"
    $Verbosity
    "--nologo"
    "-c"
    $Configuration
    if ($LocalOmniSharp) { "-property:LocalOmniSharp=true" }
)

$script:dotnetTestArgs = @("test") + $script:dotnetBuildArgs + $TestArgs + @(
    if ($TestFilter) { "--filter", $TestFilter }
    "--framework"
)

$script:IsNix = $IsLinux -or $IsMacOS
# For Apple M1, pwsh might be getting emulated, in which case we need to check
# for the proc_translated flag, otherwise we can check the architecture.
$script:IsAppleM1 = $IsMacOS -and ((sysctl -n sysctl.proc_translated 2> $null) -eq 1 -or
    [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq "Arm64")
$script:IsArm64 = -not $script:IsNix -and @("ARM64") -contains $env:PROCESSOR_ARCHITECTURE
$script:BuildInfoPath = [System.IO.Path]::Combine($PSScriptRoot, "src", "PowerShellEditorServices.Hosting", "BuildInfo.cs")
$script:PsesCommonProps = [xml](Get-Content -Raw "$PSScriptRoot/PowerShellEditorServices.Common.props")

$script:NetRuntime = @{
    PS72     = 'net6.0'
    PS73     = 'net7.0'
    Desktop  = 'net462'
    Standard = 'netstandard2.0'
}

$script:HostCoreOutput = "$PSScriptRoot/src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetRuntime.PS72)/publish"
$script:HostDeskOutput = "$PSScriptRoot/src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetRuntime.Desktop)/publish"
$script:PsesOutput = "$PSScriptRoot/src/PowerShellEditorServices/bin/$Configuration/$($script:NetRuntime.Standard)/publish"
$script:VSCodeOutput = "$PSScriptRoot/src/PowerShellEditorServices.VSCode/bin/$Configuration/$($script:NetRuntime.Standard)/publish"

if (Get-Command git -ErrorAction SilentlyContinue) {
    # ignore changes to this file
    git update-index --assume-unchanged "$PSScriptRoot/src/PowerShellEditorServices.Hosting/BuildInfo.cs"
}

Task FindDotNet {
    Assert (Get-Command dotnet -ErrorAction SilentlyContinue) "dotnet not found, please install it: https://aka.ms/dotnet-cli"

    # Strip out semantic version metadata so it can be cast to `Version`
    $existingVersion, $null = (dotnet --version) -split '-'
    Assert ([Version]$existingVersion -ge [Version]("6.0")) ".NET SDK 6.0 or higher is required, please update it: https://aka.ms/dotnet-cli"

    Write-Host "Using dotnet v$(dotnet --version) at path $((Get-Command dotnet).Source)" -ForegroundColor Green
}

Task BinClean {
    Remove-BuildItem $PSScriptRoot\.tmp
    Remove-BuildItem $PSScriptRoot\module\PowerShellEditorServices\bin
    Remove-BuildItem $PSScriptRoot\module\PowerShellEditorServices.VSCode\bin
}

Task Clean FindDotNet, BinClean, {
    Invoke-BuildExec { & dotnet clean --verbosity $Verbosity }
    Remove-BuildItem $PSScriptRoot\src\*.nupkg
    Remove-BuildItem $PSScriptRoot\PowerShellEditorServices*.zip
    Remove-BuildItem $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US\*-help.xml

    # Remove bundled component modules
    $moduleJsonPath = "$PSScriptRoot\modules.json"
    if (Test-Path $moduleJsonPath) {
        Get-Content -Raw $moduleJsonPath |
            ConvertFrom-Json |
            ForEach-Object { $_.PSObject.Properties.Name } |
            ForEach-Object { Remove-BuildItem -Path "$PSScriptRoot/module/$_" }
    }
}

Task CreateBuildInfo {
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
        Write-Host "Updating build info."
        Set-Content -LiteralPath $script:BuildInfoPath -Value $buildInfoContents -Force
    }
}

Task SetupHelpForTests {
    if (-not (Get-Help Write-Host).Examples) {
        Write-Host "Updating help for tests."
        Update-Help -Module Microsoft.PowerShell.Utility -Force -Scope CurrentUser
    }
}

Task Build FindDotNet, CreateBuildInfo, {
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -f $script:NetRuntime.Standard }
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs .\src\PowerShellEditorServices.Hosting\PowerShellEditorServices.Hosting.csproj -f $script:NetRuntime.PS72 }

    if (-not $script:IsNix) {
        Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs .\src\PowerShellEditorServices.Hosting\PowerShellEditorServices.Hosting.csproj -f $script:NetRuntime.Desktop }
    }

    # Build PowerShellEditorServices.VSCode module
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs .\src\PowerShellEditorServices.VSCode\PowerShellEditorServices.VSCode.csproj -f $script:NetRuntime.Standard }
}

Task Test TestServer, TestE2E, TestConstrainedLanguageMode

Task TestServer TestServerWinPS, TestServerPS72, TestServerPS73

# NOTE: While these can run under `pwsh.exe` we only want them to run under
# `powershell.exe` so that the CI time isn't doubled.
Task TestServerWinPS -If ($PSVersionTable.PSEdition -eq "Desktop") Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    # TODO: See https://github.com/dotnet/sdk/issues/18353 for x64 test host
    # that is debuggable! If architecture is added, the assembly path gets an
    # additional folder, necesstiating fixes to find the commands definition
    # file and test files.
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetRuntime.Desktop }
}

Task TestServerPS72 -If ($PSVersionTable.PSEdition -eq "Core") Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetRuntime.PS72 }
}

Task TestServerPS73 -If ($PSVersionTable.PSEdition -eq "Core") Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetRuntime.PS73 }
}

Task TestE2E Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\
    $env:PWSH_EXE_NAME = if ($IsCoreCLR) { "pwsh" } else { "powershell" }
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetRuntime.PS73 }
}

Task TestConstrainedLanguageMode -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\
    $env:PWSH_EXE_NAME = if ($IsCoreCLR) { "pwsh" } else { "powershell" }

    if (-not [Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown("BuiltInAdministratorsSid")) {
        Write-Warning "Skipping Constrained Language Mode tests as they must be ran in an elevated process."
        return
    }

    try {
        Write-Host "Running end-to-end tests in Constrained Language Mode."
        [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", "0x80000007", [System.EnvironmentVariableTarget]::Machine)
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetRuntime.PS73 }
    } finally {
        [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", $null, [System.EnvironmentVariableTarget]::Machine)
    }
}

Task LayoutModule -After Build {
    $modulesDir = "$PSScriptRoot/module"
    $psesVSCodeBinOutputPath = "$modulesDir/PowerShellEditorServices.VSCode/bin"
    $psesOutputPath = "$modulesDir/PowerShellEditorServices"
    $psesBinOutputPath = "$PSScriptRoot/module/PowerShellEditorServices/bin"
    $psesDepsPath = "$psesBinOutputPath/Common"
    $psesCoreHostPath = "$psesBinOutputPath/Core"
    $psesDeskHostPath = "$psesBinOutputPath/Desktop"

    foreach ($dir in $psesDepsPath, $psesCoreHostPath, $psesDeskHostPath, $psesVSCodeBinOutputPath) {
        New-Item -Force -Path $dir -ItemType Directory | Out-Null
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
            Name            = $name
            Version         = $_.Value.Version
            AllowPrerelease = $_.Value.AllowPrerelease
            Repository      = if ($_.Value.Repository) { $_.Value.Repository } else { $DefaultModuleRepository }
            Path            = $submodulePath
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
            Write-Host "`tModule '${moduleName}' already detected, skipping!"
            continue
        }

        $moduleInstallDetails = $moduleInfos[$moduleName]

        $splatParameters = @{
            Name            = $moduleName
            RequiredVersion = $moduleInstallDetails.Version
            AllowPrerelease = $moduleInstallDetails.AllowPrerelease
            Repository      = if ($moduleInstallDetails.Repository) { $moduleInstallDetails.Repository } else { $DefaultModuleRepository }
            Path            = $submodulePath
        }

        Write-Host "`tInstalling module: ${moduleName} with arguments $(ConvertTo-Json $splatParameters)"

        Save-Module @splatParameters
    }
}

Task BuildCmdletHelp -After LayoutModule {
    New-ExternalHelp -Path $PSScriptRoot\module\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US -Force | Out-Null
    New-ExternalHelp -Path $PSScriptRoot\module\PowerShellEditorServices.VSCode\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices.VSCode\en-US -Force | Out-Null
}

# The default task is to run the entire CI build
Task . Clean, Build, Test
