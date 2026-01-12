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

task RestorePsesModules {
    # NOTE: When updating module versions, ensure they are also saved to the CFS feed
    if (-not (Test-Path "module/PSScriptAnalyzer")) {
        Write-Build DarkMagenta "Restoring PSScriptAnalyzer module"
        Save-PSResource -Path module -Name PSScriptAnalyzer -Version "1.24.0" -Repository $PSRepository -TrustRepository -Verbose
    }
    if (-not (Test-Path "module/PSReadLine")) {
        Write-Build DarkMagenta "Restoring PSReadLine module"
        Save-PSResource -Path module -Name PSReadLine -Version "2.4.5" -Repository $PSRepository -TrustRepository -Verbose
    }
}

Task Build FindDotNet, CreateBuildInfo, RestorePsesModules, {
    Write-Build DarkGreen 'Building PowerShellEditorServices'
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs ./src/PowerShellEditorServices/PowerShellEditorServices.csproj -f $script:NetFramework.Standard }
    Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs ./src/PowerShellEditorServices.Hosting/PowerShellEditorServices.Hosting.csproj -f $script:NetFramework.PS74 }

    if (-not $script:IsNix) {
        Invoke-BuildExec { & dotnet publish $script:dotnetBuildArgs ./src/PowerShellEditorServices.Hosting/PowerShellEditorServices.Hosting.csproj -f $script:NetFramework.PS51 }
    }
} -If {
    $Null -eq $script:ChangesDetected -or $true -eq $script:ChangesDetected
}

Task AssembleModule -After Build {
    Write-Build DarkGreen 'Assembling PowerShellEditorServices module'
    $psesOutputPath = './module/PowerShellEditorServices'
    $psesBinOutputPath = "$psesOutputPath/bin"
    $psesDepsPath = "$psesBinOutputPath/Common"
    $psesCoreHostPath = "$psesBinOutputPath/Core"
    $psesDeskHostPath = "$psesBinOutputPath/Desktop"

    foreach ($dir in $psesDepsPath, $psesCoreHostPath, $psesDeskHostPath) {
        New-Item -Force -Path $dir -ItemType Directory | Out-Null
    }

    # Copy documents to module root
    foreach ($document in @('LICENSE', 'NOTICE.txt', 'README.md', 'SECURITY.md')) {
        Copy-Item -Force -Path $document -Destination './module'
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
    Write-Build DarkGreen 'Building cmdlet help'
    New-ExternalHelp -Path ./module/docs -OutputPath ./module/PowerShellEditorServices/Commands/en-US -Force
}

Task SetupHelpForTests {
    # Some CI do not ship with help included, and the secure devops pipeline also does not allow internet access, so we must update help from our local repository source.

    # Only commands in Microsoft.PowerShell.Archive can be tested for help so as to minimize the repository storage.
    # This requires admin rights for PS5.1

    # NOTE: You can run this task once as admin or update help separately, and continue to run tests as non-admin, if for instance developing locally.

    $installHelpScript = {
        param(
            [Parameter(Position = 0)][string]$helpPath
        )
        $PSVersion = $PSVersionTable.PSVersion
        $ErrorActionPreference = 'Stop'
        $helpPath = Resolve-Path $helpPath
        if ($PSEdition -ne 'Desktop') {
            $helpPath = Join-Path $helpPath '7'
        }

        if ((Get-Help Expand-Archive).remarks -notlike 'Get-Help cannot find the Help files*') {
            Write-Host -ForegroundColor Green "PowerShell $PSVersion Archive help is already installed"
            return
        }

        if ($PSEdition -eq 'Desktop') {
            # Cant use requires RunAsAdministrator because PS isn't smart enough to know this is a subscript.
            if (-not [Security.Principal.WindowsPrincipal]::new(
                    [Security.Principal.WindowsIdentity]::GetCurrent()
                ).IsInRole(
                    [Security.Principal.WindowsBuiltInRole]::Administrator
                )) {
                throw 'Windows PowerShell Update-Help requires admin rights. Please re-run the script in an elevated PowerShell session!'
            }
        }

        Write-Host -ForegroundColor Magenta "PowerShell $PSVersion Archive help is not installed, installing from $helpPath"

        $updateHelpParams = @{
            Module     = 'Microsoft.PowerShell.Archive'
            SourcePath = $helpPath
            UICulture  = 'en-US'
            Force      = $true
            Verbose    = $true
        }

        # PS7+ does not require admin rights if CurrentUser is used for scope. PS5.1 does not have this option.
        if ($PSEdition -ne 'Desktop') {
            $updateHelpParams.'Scope' = 'CurrentUser'
        }
        # Update the help and capture verbose output
        $updateHelpOutput = Update-Help @updateHelpParams *>&1

        if ((Get-Help Expand-Archive).remarks -like 'Get-Help cannot find the Help files*') {
            throw "Failed to install PowerShell $PSVersion Help: $updateHelpOutput"
        } else {
            Write-Host -ForegroundColor Green "PowerShell $PSVersion Archive help installed successfully"
        }
    }

    # Need this to inject the help file path since PSScriptRoot won't work inside the script
    $helpPath = Resolve-Path "$PSScriptRoot\test\PowerShellEditorServices.Test.Shared\PSHelp" -ErrorAction Stop
    Write-Build DarkMagenta "Runner help located at $helpPath"

    if (Get-Command powershell.exe -CommandType Application -ea 0) {
        Write-Build DarkMagenta 'Checking PowerShell 5.1 help'
        & powershell.exe -NoProfile -NonInteractive -Command $installHelpScript -args $helpPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to install PowerShell 5.1 help!'
        }
    }

    if ($PwshPreview -and (Get-Command $PwshPreview -ea 0)) {
        Write-Build DarkMagenta "Checking PowerShell Preview help at $PwshPreview"
        Invoke-BuildExec { & $PwshPreview -NoProfile -NonInteractive -Command $installHelpScript -args $helpPath }
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to install PowerShell Preview help!'
        }
    }

    if ($PSEdition -eq 'Core') {
        Write-Build DarkMagenta "Checking this PowerShell process's help"
        & $installHelpScript $helpPath
    }
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
        $env:PSModulePath = ''
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS51 }
    } finally {
        $env:PSModulePath = $originalModulePath
    }
}

# NOTE: The framework for the E2E tests applies to the mock client, and so
# should just be the latest supported framework.
Task TestE2EPwsh Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = 'pwsh'
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
}

if ($env:GITHUB_ACTIONS) {
    $PwshPreview = if ($script:IsNix) { "$PSScriptRoot/preview/pwsh" } else { "$PSScriptRoot/preview/pwsh.exe" }
} else {
    $PwshPreview = if ($script:IsNix) { "$HOME/.powershell-preview/pwsh" } else { "$env:LOCALAPPDATA/Microsoft/powershell-preview/pwsh.exe" }
}

Task TestE2EPreview -If (-not $env:TF_BUILD) Build, SetupHelpForTests, {
    Assert (Test-Path $PwshPreview) "PowerShell Preview not found at $PwshPreview, please install it: https://github.com/PowerShell/PowerShell/blob/master/tools/install-powershell.ps1"
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = $PwshPreview
    Write-Build DarkGreen "Running end-to-end tests with: $(& $PwshPreview --version)"
    Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
}

Task TestE2EPowerShell -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = 'powershell'
    try {
        # TODO: See https://github.com/PowerShell/vscode-powershell/issues/3886
        # Inheriting the module path for powershell.exe breaks things!
        $originalModulePath = $env:PSModulePath
        $env:PSModulePath = ''
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
    } finally {
        $env:PSModulePath = $originalModulePath
    }
}

Task TestE2EPwshCLM -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = 'pwsh'

    if (-not [Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown('BuiltInAdministratorsSid')) {
        Write-Build DarkRed 'Skipping Constrained Language Mode tests as they must be ran in an elevated process'
        return
    }

    try {
        Write-Build DarkGreen 'Running end-to-end tests in Constrained Language Mode'
        [System.Environment]::SetEnvironmentVariable('__PSLockdownPolicy', '0x80000007', [System.EnvironmentVariableTarget]::Machine)
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
    } finally {
        [System.Environment]::SetEnvironmentVariable('__PSLockdownPolicy', $null, [System.EnvironmentVariableTarget]::Machine)
    }
}

Task TestE2EPowerShellCLM -If (-not $script:IsNix) Build, SetupHelpForTests, {
    Set-Location ./test/PowerShellEditorServices.Test.E2E/
    $env:PWSH_EXE_NAME = 'powershell'

    if (-not [Security.Principal.WindowsIdentity]::GetCurrent().Owner.IsWellKnown('BuiltInAdministratorsSid')) {
        Write-Build DarkRed 'Skipping Constrained Language Mode tests as they must be ran in an elevated process'
        return
    }

    try {
        Write-Build DarkGreen 'Running end-to-end tests in Constrained Language Mode'
        [System.Environment]::SetEnvironmentVariable('__PSLockdownPolicy', '0x80000007', [System.EnvironmentVariableTarget]::Machine)
        # TODO: See https://github.com/PowerShell/vscode-powershell/issues/3886
        # Inheriting the module path for powershell.exe breaks things!
        $originalModulePath = $env:PSModulePath
        $env:PSModulePath = ''
        Invoke-BuildExec { & dotnet $script:dotnetTestArgs $script:NetFramework.PS74 }
    } finally {
        [System.Environment]::SetEnvironmentVariable('__PSLockdownPolicy', $null, [System.EnvironmentVariableTarget]::Machine)
        $env:PSModulePath = $originalModulePath
    }
}

Task BuildIfChanged.Init -Before BuildIfChanged {
    [bool]$script:ChangesDetected = $false
}

Task BuildIfChanged -Inputs {
    $slash = [IO.Path]::DirectorySeparatorChar
    Get-ChildItem ./src -Filter '*.cs' -Recurse
    | Where-Object FullName -NotLike ('*' + $slash + 'obj' + $slash + '*')
    | Where-Object FullName -NotLike ('*' + $slash + 'bin' + $slash + '*')
} -Outputs {
    './src/PowerShellEditorServices/bin/Debug/netstandard2.0/Microsoft.PowerShell.EditorServices.dll'
    './src/PowerShellEditorServices.Hosting/bin/Debug/net8.0/Microsoft.PowerShell.EditorServices.Hosting.dll'
} -Jobs {
    Write-Build DarkMagenta 'Changes detected, rebuilding'
    $script:ChangesDetected = $true
}, Build

Task Test TestPS74, TestE2EPwsh, TestPS51, TestE2EPowerShell

Task TestFull Test, TestE2EPreview, TestE2EPwshCLM, TestE2EPowerShellCLM

Task . Clean, Build, Test
