# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$LocalOmniSharp,

    [string]$PsesSubmodulePath = "$PSScriptRoot/module",

    [string]$ModulesJsonPath = "$PSScriptRoot/modules.json",

    [string]$DefaultModuleRepository = "PSGallery",

    [string]$Verbosity = "minimal",

    # See: https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
    [string]$TestFilter = '',

    # See: https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test
    [string[]]$TestArgs = @("--logger", "console;verbosity=minimal", "--logger", "trx")
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

$script:NetFramework = @{
    PS51     = 'net462'
    PS72     = 'net6.0'
    PS73     = 'net7.0'
    PS74     = 'net8.0'
    Standard = 'netstandard2.0'
}

$script:HostCoreOutput = "$PSScriptRoot/src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetFramework.PS72)/publish"
$script:HostDeskOutput = "$PSScriptRoot/src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetFramework.PS51)/publish"
$script:PsesOutput = "$PSScriptRoot/src/PowerShellEditorServices/bin/$Configuration/$($script:NetFramework.Standard)/publish"

if (Get-Command git -ErrorAction SilentlyContinue) {
    # ignore changes to this file
    git update-index --assume-unchanged "$PSScriptRoot/src/PowerShellEditorServices.Hosting/BuildInfo.cs"
}

Task FindDotNet {
    Assert (Get-Command dotnet -ErrorAction SilentlyContinue) "dotnet not found, please install it: https://aka.ms/dotnet-cli"

    # Strip out semantic version metadata so it can be cast to `Version`
    [Version]$existingVersion, $null = (dotnet --version) -split " " -split "-"
    Assert ($existingVersion -ge [Version]("8.0")) ".NET SDK 8.0 or higher is required, please update it: https://aka.ms/dotnet-cli"

    Write-Host "Using dotnet v$(dotnet --version) at path $((Get-Command dotnet).Source)" -ForegroundColor Green
}

Task BinClean {
    Remove-BuildItem $PSScriptRoot\.tmp
    Remove-BuildItem $PSScriptRoot\module\PowerShellEditorServices\bin
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
    # TODO: Check if it must be updated in a compatible way!
    Write-Host "Updating help for tests."
    Update-Help -Module Microsoft.PowerShell.Management,Microsoft.PowerShell.Utility -Force -Scope CurrentUser -UICulture en-US
}

Task Build FindDotNet, CreateBuildInfo, {
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -f $script:NetFramework.Standard }
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs .\src\PowerShellEditorServices.Hosting\PowerShellEditorServices.Hosting.csproj -f $script:NetFramework.PS72 }

    if (-not $script:IsNix) {
        Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs .\src\PowerShellEditorServices.Hosting\PowerShellEditorServices.Hosting.csproj -f $script:NetFramework.PS51 }
    }
}

# The concise set of tests (for pull requests)
Task Test TestPS74, TestE2EPwsh, TestPS51, TestE2EPowerShell

# Every combination of tests (for main branch)
Task TestFull Test, TestPS73, TestPS72, TestE2EPwshCLM, TestE2EPowerShellCLM

Task TestPS74 Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
}

Task TestPS73 Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS73 }
}

Task TestPS72 Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS72 }
}

Task TestPS51 -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test\
    # TODO: See https://github.com/dotnet/sdk/issues/18353 for x64 test host
    # that is debuggable! If architecture is added, the assembly path gets an
    # additional folder, necessitating fixes to find the commands definition
    # file and test files.
    try {
        # TODO: See https://github.com/PowerShell/vscode-powershell/issues/3886
        # Inheriting the module path for powershell.exe breaks things!
        $originalModulePath = $env:PSModulePath
        $env:PSModulePath = ""
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS51 }
    } finally {
        $env:PSModulePath = $originalModulePath
    }
}

# NOTE: The framework for the E2E tests applies to the mock client, and so
# should just be the latest supported framework.
Task TestE2EPwsh Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\
    $env:PWSH_EXE_NAME = "pwsh"
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
}

$PwshDaily = if ($script:IsNix) {
    "$HOME/.powershell-daily/pwsh"
} else {
    "$env:LOCALAPPDATA\Microsoft\powershell-daily\pwsh.exe"
}

Task TestE2EDaily -If (Test-Path $PwshDaily) Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\
    $env:PWSH_EXE_NAME = $PwshDaily
    Write-Host "Running end-to-end tests with: $(& $PwshDaily --version)"
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
}

Task TestE2EPowerShell -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\
    $env:PWSH_EXE_NAME = "powershell"
    try {
        # TODO: See https://github.com/PowerShell/vscode-powershell/issues/3886
        # Inheriting the module path for powershell.exe breaks things!
        $originalModulePath = $env:PSModulePath
        $env:PSModulePath = ""
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
    } finally {
        $env:PSModulePath = $originalModulePath
    }
}

Task TestE2EPwshCLM -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\
    $env:PWSH_EXE_NAME = "pwsh"

    if (-not [Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown("BuiltInAdministratorsSid")) {
        Write-Warning "Skipping Constrained Language Mode tests as they must be ran in an elevated process."
        return
    }

    try {
        Write-Host "Running end-to-end tests in Constrained Language Mode."
        [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", "0x80000007", [System.EnvironmentVariableTarget]::Machine)
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
    } finally {
        [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", $null, [System.EnvironmentVariableTarget]::Machine)
    }
}

Task TestE2EPowerShellCLM -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location .\test\PowerShellEditorServices.Test.E2E\
    $env:PWSH_EXE_NAME = "powershell"

    if (-not [Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown("BuiltInAdministratorsSid")) {
        Write-Warning "Skipping Constrained Language Mode tests as they must be ran in an elevated process."
        return
    }

    try {
        Write-Host "Running end-to-end tests in Constrained Language Mode."
        [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", "0x80000007", [System.EnvironmentVariableTarget]::Machine)
        # TODO: See https://github.com/PowerShell/vscode-powershell/issues/3886
        # Inheriting the module path for powershell.exe breaks things!
        $originalModulePath = $env:PSModulePath
        $env:PSModulePath = ""
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
    } finally {
        [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", $null, [System.EnvironmentVariableTarget]::Machine)
        $env:PSModulePath = $originalModulePath
    }
}

Task LayoutModule -After Build {
    $modulesDir = "$PSScriptRoot/module"
    $psesOutputPath = "$modulesDir/PowerShellEditorServices"
    $psesBinOutputPath = "$PSScriptRoot/module/PowerShellEditorServices/bin"
    $psesDepsPath = "$psesBinOutputPath/Common"
    $psesCoreHostPath = "$psesBinOutputPath/Core"
    $psesDeskHostPath = "$psesBinOutputPath/Desktop"

    foreach ($dir in $psesDepsPath, $psesCoreHostPath, $psesDeskHostPath) {
        New-Item -Force -Path $dir -ItemType Directory | Out-Null
    }

    # Copy third party notices to module folder
    Copy-Item -Force -Path "$PSScriptRoot\NOTICE.txt" -Destination $psesOutputPath

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
            Repository      = if ($moduleInstallDetails.Repository) { $moduleInstallDetails.Repository } else { $DefaultModuleRepository }
            Path            = $submodulePath
        }

        # There's a bug in PowerShell get where this argument isn't correctly translated when it's false.
        if ($moduleInstallDetails.AllowPrerelease) {
            $splatParameters["AllowPrerelease"] = $moduleInstallDetails.AllowPrerelease
        }

        Write-Host "`tInstalling module: ${moduleName} with arguments $(ConvertTo-Json $splatParameters)"

        Save-Module @splatParameters
    }
}

Task BuildCmdletHelp -After LayoutModule {
    New-ExternalHelp -Path $PSScriptRoot\module\docs -OutputPath $PSScriptRoot\module\PowerShellEditorServices\Commands\en-US -Force | Out-Null
}

# The default task is to run the entire CI build
Task . Clean, Build, Test
