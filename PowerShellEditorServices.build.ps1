# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$LocalOmniSharp,

    [string]$PSRepository = "PSGallery",

    [string]$Verbosity = "minimal",

    # See: https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
    [string]$TestFilter = '',

    # See: https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test
    [string[]]$TestArgs = @("--logger", "console;verbosity=minimal", "--logger", "trx")
)

#Requires -Modules @{ModuleName = "InvokeBuild"; ModuleVersion = "5.0.0"}
#Requires -Modules @{ModuleName = "platyPS"; ModuleVersion = "0.14.2"}

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
$script:BuildInfoPath = "src/PowerShellEditorServices.Hosting/BuildInfo.cs"

$script:NetFramework = @{
    PS51     = 'net462'
    PS74     = 'net8.0'
    Standard = 'netstandard2.0'
}

$script:HostCoreOutput = "src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetFramework.PS74)/publish"
$script:HostDeskOutput = "src/PowerShellEditorServices.Hosting/bin/$Configuration/$($script:NetFramework.PS51)/publish"
$script:PsesOutput = "src/PowerShellEditorServices/bin/$Configuration/$($script:NetFramework.Standard)/publish"

if (Get-Command git -ErrorAction SilentlyContinue) {
    # ignore changes to this file
    git update-index --assume-unchanged $script:BuildInfoPath
}

Task FindDotNet {
    Assert (Get-Command dotnet -ErrorAction SilentlyContinue) "dotnet not found, please install it: https://aka.ms/dotnet-cli"

    # Strip out semantic version metadata so it can be cast to `Version`
    [Version]$existingVersion, $null = (dotnet --version) -split " " -split "-"
    Assert ($existingVersion -ge [Version]("8.0")) ".NET SDK 8.0 or higher is required, please update it: https://aka.ms/dotnet-cli"

    Write-Build DarkGreen "Using dotnet v$(dotnet --version) at path $((Get-Command dotnet).Source)"
}

Task Clean FindDotNet, {
    Write-Build DarkMagenta "Cleaning PowerShellEditorServices"
    Invoke-BuildExec { & dotnet clean --verbosity $Verbosity }
    Remove-BuildItem module/PowerShellEditorServices/bin
    Remove-BuildItem module/PowerShellEditorServices/Commands/en-US/*-help.xml
    Remove-BuildItem module/PSReadLine
    Remove-BuildItem module/PSScriptAnalyzer
}

Task CreateBuildInfo {
    $buildOrigin = "Development"
    $buildCommit = git rev-parse HEAD

    [xml]$xml = Get-Content "PowerShellEditorServices.Common.props"
    $buildVersion = $xml.Project.PropertyGroup.VersionPrefix
    $prerelease = $xml.Project.PropertyGroup.VersionSuffix
    if ($prerelease) { $buildVersion += "-$prerelease" }

    # Set build info fields on build platforms
    if ($env:TF_BUILD) { # Azure DevOps AKA OneBranch
        if ($env:BUILD_REASON -like "Manual") {
            $buildOrigin = "Release"
        } else {
            $buildOrigin = "AzureDevOps-CI"
        }
    } elseif ($env:GITHUB_ACTIONS) {
        $buildOrigin = "GitHub-CI"
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
        Write-Build DarkMagenta "Updating build info"
        Set-Content -LiteralPath $script:BuildInfoPath -Value $buildInfoContents -Force
    }
}

task RestorePsesModules -If (-not (Test-Path "module/PSReadLine") -or -not (Test-Path "module/PSScriptAnalyzer")) {
    Write-Build DarkMagenta "Restoring bundled modules"
    Save-Module -Path module -Repository $PSRepository -Name PSScriptAnalyzer -RequiredVersion "1.23.0" -Verbose
    Save-Module -Path module -Repository $PSRepository -Name PSReadLine -RequiredVersion "2.4.0-beta0" -AllowPrerelease -Verbose
}

Task Build FindDotNet, CreateBuildInfo, RestorePsesModules, {
    Write-Build DarkGreen "Building PowerShellEditorServices"
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs ./src/PowerShellEditorServices/PowerShellEditorServices.csproj -f $script:NetFramework.Standard }
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs ./src/PowerShellEditorServices.Hosting/PowerShellEditorServices.Hosting.csproj -f $script:NetFramework.PS74 }

    if (-not $script:IsNix) {
        Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs ./src/PowerShellEditorServices.Hosting/PowerShellEditorServices.Hosting.csproj -f $script:NetFramework.PS51 }
    }
}

Task AssembleModule -After Build {
    Write-Build DarkGreen "Assembling PowerShellEditorServices module"
    $psesOutputPath = "./module/PowerShellEditorServices"
    $psesBinOutputPath = "$psesOutputPath/bin"
    $psesDepsPath = "$psesBinOutputPath/Common"
    $psesCoreHostPath = "$psesBinOutputPath/Core"
    $psesDeskHostPath = "$psesBinOutputPath/Desktop"

    foreach ($dir in $psesDepsPath, $psesCoreHostPath, $psesDeskHostPath) {
        New-Item -Force -Path $dir -ItemType Directory | Out-Null
    }

    # Copy documents to module root
    foreach ($document in @("LICENSE", "NOTICE.txt", "README.md", "SECURITY.md")) {
        Copy-Item -Force -Path $document -Destination "./module"
    }

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

Task BuildCmdletHelp -After AssembleModule {
    Write-Build DarkGreen "Building cmdlet help"
    New-ExternalHelp -Path ./module/docs -OutputPath ./module/PowerShellEditorServices/Commands/en-US -Force
}

Task SetupHelpForTests {
    Write-Build DarkMagenta "Updating help (for tests)"
    Update-Help -Module Microsoft.PowerShell.Management,Microsoft.PowerShell.Utility -Force -Scope CurrentUser -UICulture en-US
}

Task TestPS74 Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test/
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
}

Task TestPS51 -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test/
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
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = "pwsh"
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
}

$PwshDaily = if ($script:IsNix) {
    "$HOME/.powershell-daily/pwsh"
} else {
    "$env:LOCALAPPDATA/Microsoft/powershell-daily/pwsh.exe"
}

Task TestE2EDaily -If (Test-Path $PwshDaily) Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = $PwshDaily
    Write-Build DarkGreen "Running end-to-end tests with: $(& $PwshDaily --version)"
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
}

Task TestE2EPowerShell -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
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
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = "pwsh"

    if (-not [Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown("BuiltInAdministratorsSid")) {
        Write-Build DarkRed "Skipping Constrained Language Mode tests as they must be ran in an elevated process"
        return
    }

    try {
        Write-Build DarkGreen "Running end-to-end tests in Constrained Language Mode"
        [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", "0x80000007", [System.EnvironmentVariableTarget]::Machine)
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
    } finally {
        [System.Environment]::SetEnvironmentVariable("__PSLockdownPolicy", $null, [System.EnvironmentVariableTarget]::Machine)
    }
}

Task TestE2EPowerShellCLM -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = "powershell"

    if (-not [Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown("BuiltInAdministratorsSid")) {
        Write-Build DarkRed "Skipping Constrained Language Mode tests as they must be ran in an elevated process"
        return
    }

    try {
        Write-Build DarkGreen "Running end-to-end tests in Constrained Language Mode"
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

Task Test TestPS74, TestE2EPwsh, TestPS51, TestE2EPowerShell

Task TestFull Test, TestE2EDaily, TestE2EPwshCLM, TestE2EPowerShellCLM

Task . Clean, Build, Test
