#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#


    Microsoft.PowerShell.Utility\Add-Type -Path "$PSScriptRoot/bin/Core/Microsoft.PowerShell.EditorServices.dll"
    Microsoft.PowerShell.Utility\Add-Type -Path "$PSScriptRoot/bin/Core/Microsoft.PowerShell.EditorServices.Protocol.dll"
    Microsoft.PowerShell.Utility\Add-Type -Path "$PSScriptRoot/bin/Core/Microsoft.PowerShell.EditorServices.Host.dll"


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

        [int]
        $LanguageServicePort,

        [int]
        $DebugServicePort,

        [switch]
        $Stdio,

        [string]
        $LanguageServiceNamedPipe,

        [string]
        $DebugServiceNamedPipe,

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
        $DebugServiceOnly,

        [string[]]
        $AdditionalModules = @(),

        [string[]]
        [ValidateNotNull()]
        $FeatureFlags = @(),

        [switch]
        $WaitForDebugger
    )

    $editorServicesHost = $null
    $hostDetails =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Session.HostDetails @(
            $HostName,
            $HostProfileId,
            (Microsoft.PowerShell.Utility\New-Object System.Version @($HostVersion)))

    $editorServicesHost =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Host.EditorServicesHost @(
            $hostDetails,
            $BundledModulesPath,
            $EnableConsoleRepl.IsPresent,
            $WaitForDebugger.IsPresent,
            $AdditionalModules,
            $FeatureFlags)

    # Build the profile paths using the root paths of the current $profile variable
    $profilePaths =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Session.ProfilePaths @(
            $hostDetails.ProfileId,
            [System.IO.Path]::GetDirectoryName($profile.AllUsersAllHosts),
            [System.IO.Path]::GetDirectoryName($profile.CurrentUserAllHosts))

    $editorServicesHost.StartLogging($LogPath, $LogLevel);

    $languageServiceConfig =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Host.EditorServiceTransportConfig

    $debugServiceConfig =
        Microsoft.PowerShell.Utility\New-Object Microsoft.PowerShell.EditorServices.Host.EditorServiceTransportConfig

    if ($Stdio.IsPresent) {
        $languageServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Host.EditorServiceTransportType]::Stdio
        $debugServiceConfig.TransportType    = [Microsoft.PowerShell.EditorServices.Host.EditorServiceTransportType]::Stdio
    }

    if ($LanguageServicePort) {
        $languageServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Host.EditorServiceTransportType]::Tcp
        $languageServiceConfig.Endpoint = "$LanguageServicePort"
    }

    if ($DebugServicePort) {
        $debugServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Host.EditorServiceTransportType]::Tcp
        $debugServiceConfig.Endpoint = "$DebugServicePort"
    }

    if ($LanguageServiceNamedPipe) {
        $languageServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Host.EditorServiceTransportType]::NamedPipe
        $languageServiceConfig.Endpoint = "$LanguageServiceNamedPipe"
    }

    if ($DebugServiceNamedPipe) {
        $debugServiceConfig.TransportType = [Microsoft.PowerShell.EditorServices.Host.EditorServiceTransportType]::NamedPipe
        $debugServiceConfig.Endpoint = "$DebugServiceNamedPipe"
    }

    if ($DebugServiceOnly.IsPresent) {
        $editorServicesHost.StartDebugService($debugServiceConfig, $profilePaths, $false);
    } else {
        $editorServicesHost.StartLanguageService($languageServiceConfig, $profilePaths);
        $editorServicesHost.StartDebugService($debugServiceConfig, $profilePaths, $true);
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
