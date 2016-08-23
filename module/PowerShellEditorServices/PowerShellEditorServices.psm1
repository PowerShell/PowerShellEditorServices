if (!$PSVersionTable.PSEdition -or $PSVersionTable.PSEdition -eq "Desktop") {
    Add-Type -Path "$PSScriptRoot/bin/Desktop/Microsoft.PowerShell.EditorServices.dll"
    Add-Type -Path "$PSScriptRoot/bin/Desktop/Microsoft.PowerShell.EditorServices.Host.dll"
}
else {
    Add-Type -Path "$PSScriptRoot/bin/Nano/Microsoft.PowerShell.EditorServices.Nano.dll"
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
        $WaitForDebugger
    )

    $editorServicesHost = $null
    $hostDetails = New-Object Microsoft.PowerShell.EditorServices.Session.HostDetails @($HostName, $HostProfileId, (New-Object System.Version @($HostVersion)))

    try {
        $editorServicesHost =
            New-Object Microsoft.PowerShell.EditorServices.Host.EditorServicesHost @(
                $hostDetails,
                $BundledModulesPath,
                $WaitForDebugger.IsPresent)

        # Build the profile paths using the root paths of the current $profile variable
        $profilePaths = New-Object Microsoft.PowerShell.EditorServices.Session.ProfilePaths @(
            $hostDetails.ProfileId,
            [System.IO.Path]::GetDirectoryName($profile.AllUsersAllHosts),
            [System.IO.Path]::GetDirectoryName($profile.CurrentUserAllHosts));

        $editorServicesHost.StartLogging($LogPath, $LogLevel);
        $editorServicesHost.StartLanguageService($LanguageServicePort, $profilePaths);
        $editorServicesHost.StartDebugService($DebugServicePort, $profilePaths);
    }
    catch {
        Write-Error "PowerShell Editor Services host initialization failed, terminating."
        Write-Error $_.Exception
    }

    return $editorServicesHost
}