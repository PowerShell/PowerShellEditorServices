if (!$PSVersionTable.PSEdition -or $PSVersionTable.PSEdition -eq "Desktop") {
    Add-Type -Path "$PSScriptRoot/bin/Desktop/Microsoft.PowerShell.EditorServices.dll"
    Add-Type -Path "$PSScriptRoot/bin/Desktop/Microsoft.PowerShell.EditorServices.Host.dll"
}
else {
    Add-Type -Path "$PSScriptRoot/bin/Core/Microsoft.PowerShell.EditorServices.dll"
    Add-Type -Path "$PSScriptRoot/bin/Core/Microsoft.PowerShell.EditorServices.Host.dll"
}

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

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [int]
        $LanguageServicePort,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [int]
        $DebugServicePort,

        [ValidateNotNullOrEmpty()]
        [string]
        $BundledModulesPath,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        $LogPath,

        [ValidateSet("Normal", "Verbose", "Error")]
        $LogLevel = "Normal",

        [switch]
        $EnableConsoleRepl,

        [string]
        $DebugServiceOnly,

        [string[]]
        [ValidateNotNull()]
        $FeatureFlags = @(),

        [switch]
        $WaitForDebugger
    )

    $editorServicesHost = $null
    $hostDetails = New-Object Microsoft.PowerShell.EditorServices.Session.HostDetails @($HostName, $HostProfileId, (New-Object System.Version @($HostVersion)))

    try {
        $editorServicesHost =
            New-Object Microsoft.PowerShell.EditorServices.Host.EditorServicesHost @(
                $hostDetails,
                $BundledModulesPath,
                $EnableConsoleRepl.IsPresent,
                $WaitForDebugger.IsPresent,
                $FeatureFlags)

        # Build the profile paths using the root paths of the current $profile variable
        $profilePaths = New-Object Microsoft.PowerShell.EditorServices.Session.ProfilePaths @(
            $hostDetails.ProfileId,
            [System.IO.Path]::GetDirectoryName($profile.AllUsersAllHosts),
            [System.IO.Path]::GetDirectoryName($profile.CurrentUserAllHosts));

        $editorServicesHost.StartLogging($LogPath, $LogLevel);

        if ($DebugServiceOnly.IsPresent) {
            $editorServicesHost.StartDebugService($DebugServicePort, $profilePaths, $false);
        }
        else {
            $editorServicesHost.StartLanguageService($LanguageServicePort, $profilePaths);
            $editorServicesHost.StartDebugService($DebugServicePort, $profilePaths, $true);
        }
    }
    catch {
        Write-Error "PowerShell Editor Services host initialization failed, terminating."
        Write-Error $_.Exception
    }

    return $editorServicesHost
}

function Get-PowerShellEditorServicesVersion {
    $nl = [System.Environment]::NewLine

    $versionInfo = "PSVersionTable:`n$($PSVersionTable | Out-String)" -replace '\n$', ''

    if ($IsLinux) {
        $versionInfo += "Linux version: $(lsb_release -d)$nl"
    }
    elseif ($IsOSX) {
        $versionInfo += "macOS version: $(lsb_release -d)$nl"
    }
    else {
        $versionInfo += "Windows version: $(Get-CimInstance Win32_OperatingSystem | Foreach-Object Version)$nl"
    }

    $versionInfo += $nl

    $OFS = ", "
    $versionInfo += "VSCode version: $(code -v)$nl"
    $OFS = "$nl    "
    $versionInfo += "VSCode extensions:$nl    $(code --list-extensions --show-versions)"

    if (!$IsLinux -and !$IsOSX) {
        $versionInfo | Microsoft.PowerShell.Management\Set-Clipboard
    }

    $versionInfo
}
