#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Need to load pipe handling shim assemblies in Windows PowerShell and PSCore 6.0 because they don't have WCP
if ($PSEdition -eq 'Desktop') {
    Microsoft.PowerShell.Utility\Add-Type -Path "$PSScriptRoot/bin/Desktop/System.IO.Pipes.AccessControl.dll"
    Microsoft.PowerShell.Utility\Add-Type -Path "$PSScriptRoot/bin/Desktop/System.Security.AccessControl.dll"
    Microsoft.PowerShell.Utility\Add-Type -Path "$PSScriptRoot/bin/Desktop/System.Security.Principal.Windows.dll"
}

Microsoft.PowerShell.Utility\Add-Type -Path "$PSScriptRoot/bin/Microsoft.PowerShell.EditorServices.dll"

function Start-EditorServicesHost {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $HostName,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $HostProfileId,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $HostVersion,

        [Parameter(ParameterSetName="Stdio",Mandatory=$true)]
        [switch]
        $Stdio,

        [Parameter(ParameterSetName="NamedPipe",Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $LanguageServiceNamedPipe,

        [Parameter(ParameterSetName="NamedPipe")]
        [string]
        $DebugServiceNamedPipe,

        [Parameter(ParameterSetName="NamedPipeSimplex",Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $LanguageServiceInNamedPipe,

        [Parameter(ParameterSetName="NamedPipeSimplex",Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $LanguageServiceOutNamedPipe,

        [Parameter(ParameterSetName="NamedPipeSimplex")]
        [string]
        $DebugServiceInNamedPipe,

        [Parameter(ParameterSetName="NamedPipeSimplex")]
        [string]
        $DebugServiceOutNamedPipe,

        [ValidateNotNullOrEmpty()]
        [string]
        $BundledModulesPath,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        $LogPath,

        [ValidateSet("Normal", "Verbose", "Error", "Diagnostic")]
        $LogLevel = "Normal",

        [switch]
        $EnableConsoleRepl,

        [switch]
        $UseLegacyReadLine,

        [switch]
        $DebugServiceOnly,

        [string[]]
        $AdditionalModules = @(),

        [string[]]
        [ValidateNotNull()]
        $FeatureFlags = @(),

        [switch]
        $WaitForDebugger
    )

    # Make sure PSScriptAnalyzer dlls are loaded.
    Import-Module PSScriptAnalyzer

    $editorServicesHost = $null
    $hostDetails =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Hosting.HostDetails @(
            $HostName,
            $HostProfileId,
            (Microsoft.PowerShell.Utility\New-Object System.Version @($HostVersion)))

    $editorServicesHost =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Hosting.EditorServicesHost @(
            $hostDetails,
            $BundledModulesPath,
            $EnableConsoleRepl.IsPresent,
            $UseLegacyReadLine.IsPresent,
            $WaitForDebugger.IsPresent,
            $AdditionalModules,
            $FeatureFlags,
            $Host)

    # Build the profile paths using the root paths of the current $profile variable
    $profilePaths =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Hosting.ProfilePaths @(
            $hostDetails.ProfileId,
            [System.IO.Path]::GetDirectoryName($profile.AllUsersAllHosts),
            [System.IO.Path]::GetDirectoryName($profile.CurrentUserAllHosts))

    $editorServicesHost.StartLogging($LogPath, $LogLevel);

    $languageServiceConfig =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Hosting.EditorServiceTransportConfig

    $debugServiceConfig =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Hosting.EditorServiceTransportConfig

    switch ($PSCmdlet.ParameterSetName) {
        "Stdio" {
            $languageServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Hosting.EditorServiceTransportType]::Stdio
            $debugServiceConfig.TransportType    = [Microsoft.PowerShell.EditorServices.Hosting.EditorServiceTransportType]::Stdio
            break
        }
        "NamedPipe" {
            $languageServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Hosting.EditorServiceTransportType]::NamedPipe
            $languageServiceConfig.InOutPipeName = "$LanguageServiceNamedPipe"
            if ($DebugServiceNamedPipe) {
                $debugServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Hosting.EditorServiceTransportType]::NamedPipe
                $debugServiceConfig.InOutPipeName = "$DebugServiceNamedPipe"
            }
            break
        }
        "NamedPipeSimplex" {
            $languageServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Hosting.EditorServiceTransportType]::NamedPipe
            $languageServiceConfig.InPipeName = $LanguageServiceInNamedPipe
            $languageServiceConfig.OutPipeName = $LanguageServiceOutNamedPipe
            if ($DebugServiceInNamedPipe -and $DebugServiceOutNamedPipe) {
                $debugServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Hosting.EditorServiceTransportType]::NamedPipe
                $debugServiceConfig.InPipeName = $DebugServiceInNamedPipe
                $debugServiceConfig.OutPipeName = $DebugServiceOutNamedPipe
            }
            break
        }
    }

    if ($DebugServiceOnly.IsPresent) {
        $editorServicesHost.StartDebugService($debugServiceConfig, $profilePaths, $true);
    } elseif($Stdio.IsPresent) {
        $editorServicesHost.StartLanguageService($languageServiceConfig, $profilePaths);
    } else {
        $editorServicesHost.StartLanguageService($languageServiceConfig, $profilePaths);
        $editorServicesHost.StartDebugService($debugServiceConfig, $profilePaths, $false);
    }

    return $editorServicesHost
}

function Compress-LogDir {
    [CmdletBinding(SupportsShouldProcess=$true)]
    param (
        [Parameter(Mandatory=$true, Position=0, HelpMessage="Literal path to a log directory.")]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path
    )

    begin {
        function LegacyZipFolder($Path, $ZipPath) {
            if (!(Microsoft.PowerShell.Management\Test-Path($ZipPath))) {
                $zipMagicHeader = "PK" + [char]5 + [char]6 + ("$([char]0)" * 18)
                Microsoft.PowerShell.Management\Set-Content -LiteralPath $ZipPath -Value $zipMagicHeader
                (Microsoft.PowerShell.Management\Get-Item $ZipPath).IsReadOnly = $false
            }

            $shellApplication = Microsoft.PowerShell.Utility\New-Object -ComObject Shell.Application
            $zipPackage = $shellApplication.NameSpace($ZipPath)

            foreach ($file in (Microsoft.PowerShell.Management\Get-ChildItem -LiteralPath $Path)) {
                $zipPackage.CopyHere($file.FullName)
                Start-Sleep -MilliSeconds 500
            }
        }
    }

    end {
        $zipPath = ((Microsoft.PowerShell.Management\Convert-Path $Path) -replace '(\\|/)$','') + ".zip"

        if (Get-Command Microsoft.PowerShell.Archive\Compress-Archive) {
            if ($PSCmdlet.ShouldProcess($zipPath, "Create ZIP")) {
                Microsoft.PowerShell.Archive\Compress-Archive -LiteralPath $Path -DestinationPath $zipPath -Force -CompressionLevel Optimal
                $zipPath
            }
        }
        else {
            if ($PSCmdlet.ShouldProcess($zipPath, "Create Legacy ZIP")) {
                LegacyZipFolder $Path $zipPath
                $zipPath
            }
        }
    }
}

function Get-PowerShellEditorServicesVersion {
    $nl = [System.Environment]::NewLine

    $versionInfo = "PSES module version: $($MyInvocation.MyCommand.Module.Version)$nl"

    $versionInfo += "PSVersion:           $($PSVersionTable.PSVersion)$nl"
    if ($PSVersionTable.PSEdition) {
        $versionInfo += "PSEdition:           $($PSVersionTable.PSEdition)$nl"
    }
    $versionInfo += "PSBuildVersion:      $($PSVersionTable.BuildVersion)$nl"
    $versionInfo += "CLRVersion:          $($PSVersionTable.CLRVersion)$nl"

    $versionInfo += "Operating system:    "
    if ($IsLinux) {
        $versionInfo += "Linux $(lsb_release -d -s)$nl"
    }
    elseif ($IsOSX) {
        $versionInfo += "macOS $(lsb_release -d -s)$nl"
    }
    else {
        $osInfo = CimCmdlets\Get-CimInstance Win32_OperatingSystem
        $versionInfo += "Windows $($osInfo.OSArchitecture) $($osInfo.Version)$nl"
    }

    $versionInfo
}
