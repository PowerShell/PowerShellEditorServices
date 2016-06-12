if (!$PSVersionTable.PSEdition -or $PSVersionTable.PSEdition -eq "Desktop") {
    Add-Type -Path "$PSScriptRoot\bin\Desktop\Microsoft.PowerShell.EditorServices.dll"
    Add-Type -Path "$PSScriptRoot\bin\Desktop\Microsoft.PowerShell.EditorServices.Host.dll"
}
else {
    Add-Type -Path "$PSScriptRoot\bin\Nano\Microsoft.PowerShell.EditorServices.Nano.dll"
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
        [string]
        $LanguageServicePipeName,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $DebugServicePipeName,

        [ValidateNotNullOrEmpty()]
        [string]
        $BundledModulesPath,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        $LogPath,

        [ValidateSet("Normal", "Verbose", "Error")]
        $LogLevel = "Normal",

        [switch]
        $WaitForCompletion,

        [switch]
        $WaitForDebugger
    )

    $editorServicesHost = $null
    $hostDetails = [Microsoft.PowerShell.EditorServices.Session.HostDetails]::new($HostName, $HostProfileId, [System.Version]::new($HostVersion))

    try {
        $editorServicesHost =
            [Microsoft.PowerShell.EditorServices.Host.EditorServicesHost]::new(
                $hostDetails,
                $BundledModulesPath,
                $WaitForDebugger.IsPresent);

        # Build the profile paths using the root paths of the current $profile variable
        $profilePaths = [Microsoft.PowerShell.EditorServices.Session.ProfilePaths]::new(
            $hostDetails.ProfileId,
            [System.IO.Path]::GetDirectoryName($profile.AllUsersAllHosts),
            [System.IO.Path]::GetDirectoryName($profile.CurrentUserAllHosts));

        $editorServicesHost.StartLogging($LogPath, $LogLevel);
        $editorServicesHost.StartLanguageService($LanguageServicePipeName, $profilePaths);
        $editorServicesHost.StartDebugService($DebugServicePipeName, $profilePaths);

        Write-Output "PowerShell Editor Services host has started."

        if ($WaitForCompletion.IsPresent) {
            $editorServicesHost.WaitForCompletion();
        }
    }
    catch {
        Write-Error "PowerShell Editor Services host initialization failed, terminating."
        Write-Error $_.Exception
    }

    return $editorServicesHost
}
