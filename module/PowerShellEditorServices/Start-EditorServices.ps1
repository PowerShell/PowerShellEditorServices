# PowerShell Editor Services Bootstrapper Script
# ----------------------------------------------
# This script contains startup logic for the PowerShell Editor Services
# module when launched by an editor.  It handles the following tasks:
#
# - Verifying the existence of dependencies like PowerShellGet
# - Verifying that the expected version of the PowerShellEditorServices module is installed
# - Installing the PowerShellEditorServices module if confirmed by the user
# - Creating named pipes for the language and debug services to use (if using named pipes)
# - Starting the language and debug services from the PowerShellEditorServices module
#
# NOTE: If editor integration authors make modifications to this
#       script, please consider contributing changes back to the
#       canonical version of this script at the PowerShell Editor
#       Services GitHub repository:
#
#       https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/PowerShellEditorServices/Start-EditorServices.ps1

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

    [ValidateNotNullOrEmpty()]
    [string]
    $BundledModulesPath,

    [ValidateNotNullOrEmpty()]
    $LogPath,

    [ValidateSet("Diagnostic", "Normal", "Verbose", "Error")]
    $LogLevel,

	[Parameter(Mandatory=$true)]
	[ValidateNotNullOrEmpty()]
	[string]
	$SessionDetailsPath,

    [switch]
    $EnableConsoleRepl,

    [switch]
    $DebugServiceOnly,

    [string[]]
    $AdditionalModules,

    [string[]]
    $FeatureFlags,

    [switch]
    $WaitForDebugger,

    [switch]
    $ConfirmInstall,

    [switch]
    $Stdio,

    [string]
    $LanguageServicePipeName = $null,

    [string]
    $DebugServicePipeName = $null
)

$DEFAULT_USER_MODE = "600"

if ($LogLevel -eq "Diagnostic") {
    if (!$Stdio.IsPresent) {
        $VerbosePreference = 'Continue'
    }
    $scriptName = [System.IO.Path]::GetFileNameWithoutExtension($MyInvocation.MyCommand.Name)
    $logFileName = [System.IO.Path]::GetFileName($LogPath)
    Start-Transcript (Join-Path (Split-Path $LogPath -Parent) "$scriptName-$logFileName") -Force | Out-Null
}

function LogSection([string]$msg) {
    Write-Verbose "`n#-- $msg $('-' * ([Math]::Max(0, 73 - $msg.Length)))"
}

function Log([string[]]$msg) {
    $msg | Write-Verbose
}

function ExitWithError($errorString) {
    Write-Host -ForegroundColor Red "`n`n$errorString"

    # Sleep for a while to make sure the user has time to see and copy the
    # error message
    Start-Sleep -Seconds 300

    exit 1;
}

function WriteSessionFile($sessionInfo) {
    $sessionInfoJson = Microsoft.PowerShell.Utility\ConvertTo-Json -InputObject $sessionInfo -Compress
    Log "Writing session file with contents:"
    Log $sessionInfoJson
    $sessionInfoJson | Microsoft.PowerShell.Management\Set-Content -Force -Path "$SessionDetailsPath" -ErrorAction Stop
}

# Are we running in PowerShell 2 or earlier?
if ($PSVersionTable.PSVersion.Major -le 2) {
    # No ConvertTo-Json on PSv2 and below, so write out the JSON manually
    "{`"status`": `"failed`", `"reason`": `"unsupported`", `"powerShellVersion`": `"$($PSVersionTable.PSVersion.ToString())`"}" |
        Microsoft.PowerShell.Management\Set-Content -Force -Path "$SessionDetailsPath" -ErrorAction Stop

    ExitWithError "Unsupported PowerShell version $($PSVersionTable.PSVersion), language features are disabled."
}


if ($host.Runspace.LanguageMode -eq 'ConstrainedLanguage') {
    WriteSessionFile @{
        "status" = "failed"
        "reason" = "languageMode"
        "detail" = $host.Runspace.LanguageMode.ToString()
    }

    ExitWithError "PowerShell is configured with an unsupported LanguageMode (ConstrainedLanguage), language features are disabled."
}

# Are we running in PowerShell 5 or later?
$isPS5orLater = $PSVersionTable.PSVersion.Major -ge 5

# If PSReadline is present in the session, remove it so that runspace
# management is easier
if ((Microsoft.PowerShell.Core\Get-Module PSReadline).Count -gt 0) {
    LogSection "Removing PSReadLine module"
    Microsoft.PowerShell.Core\Remove-Module PSReadline -ErrorAction SilentlyContinue
}

# This variable will be assigned later to contain information about
# what happened while attempting to launch the PowerShell Editor
# Services host
$resultDetails = $null;

function Test-ModuleAvailable($ModuleName, $ModuleVersion) {
    Log "Testing module availability $ModuleName $ModuleVersion"

    $modules = Microsoft.PowerShell.Core\Get-Module -ListAvailable $moduleName
    if ($modules -ne $null) {
        if ($ModuleVersion -ne $null) {
            foreach ($module in $modules) {
                if ($module.Version.Equals($moduleVersion)) {
                    Log "$ModuleName $ModuleVersion found"
                    return $true;
                }
            }
        }
        else {
            Log "$ModuleName $ModuleVersion found"
            return $true;
        }
    }

    Log "$ModuleName $ModuleVersion NOT found"
    return $false;
}

function New-NamedPipeName {

    # We try 10 times to find a valid pipe name
    for ($i = 0; $i -lt 10; $i++) {
        # add a guid to make the pipe unique
        $PipeName = "PSES_$([guid]::NewGuid())"

        if ((Test-NamedPipeName -PipeName $PipeName)) {
            return $PipeName
        }
    }
    ExitWithError "Could not find valid a pipe name."
}

function Get-NamedPipePath {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $PipeName
    )

    if (-not $IsLinux -and -not $IsMacOS) {
        return "\\.\pipe\$PipeName";
    }
    else {
        # Windows uses NamedPipes where non-Windows platforms use Unix Domain Sockets.
        # the Unix Domain Sockets live in the tmp directory and are prefixed with "CoreFxPipe_"
        return (Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "CoreFxPipe_$PipeName")
    }

}

# Returns True if it's a valid pipe name
# A valid pipe name is a file that does not exist either
# in the temp directory (macOS & Linux) or in the pipe directory (Windows)
function Test-NamedPipeName {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $PipeName
    )

    $path = Get-NamedPipePath -PipeName $PipeName
    return -not (Test-Path $path)
}

function Set-NamedPipeMode {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $PipeFile
    )

    if ($IsWindows) {
        return
    }

    chmod $DEFAULT_USER_MODE $PipeFile

    if ($IsLinux) {
        $mode = stat -c "%a" $PipeFile
    }
    elseif ($IsMacOS) {
        $mode = stat -f "%A" $PipeFile
    }

    if ($mode -ne $DEFAULT_USER_MODE) {
        ExitWithError "Permissions to the pipe file were not set properly. Expected: $DEFAULT_USER_MODE Actual: $mode for file: $PipeFile"
    }
}

# Add BundledModulesPath to $env:PSModulePath
if ($BundledModulesPath) {
    $env:PSModulePath = $env:PSModulePath.TrimEnd([System.IO.Path]::PathSeparator) + [System.IO.Path]::PathSeparator + $BundledModulesPath
    LogSection "Updated PSModulePath to:"
    Log ($env:PSModulePath -split [System.IO.Path]::PathSeparator)
}

LogSection "Check required modules available"
# Check if PowerShellGet module is available
if ((Test-ModuleAvailable "PowerShellGet") -eq $false) {
    Log "Failed to find PowerShellGet module"
    # TODO: WRITE ERROR
}

try {
    LogSection "Start up PowerShellEditorServices"
    Log "Importing PowerShellEditorServices"

    Microsoft.PowerShell.Core\Import-Module PowerShellEditorServices -ErrorAction Stop

    # Locate available port numbers for services
    # There could be only one service on Stdio channel

    $languageServiceTransport = $null
    $debugServiceTransport = $null

    if ($Stdio.IsPresent) {
        $languageServiceTransport = "Stdio"
        $debugServiceTransport = "Stdio"
    }
    else {
        $languageServiceTransport = "NamedPipe"
        $debugServiceTransport = "NamedPipe"
        if (-not $LanguageServicePipeName) {
            $LanguageServicePipeName = New-NamedPipeName
        }
        else {
            if (-not (Test-NamedPipeName -PipeName $LanguageServicePipeName)) {
                ExitWithError "Pipe name supplied is already taken: $LanguageServicePipeName"
            }
        }
        if (-not $DebugServicePipeName) {
            $DebugServicePipeName = New-NamedPipeName
        }
        else {
            if (-not (Test-NamedPipeName -PipeName $DebugServicePipeName)) {
                ExitWithError "Pipe name supplied is already taken: $DebugServicePipeName"
            }
        }
    }

    if ($EnableConsoleRepl) {
        Write-Host "PowerShell Integrated Console`n"
    }

    # Create the Editor Services host
    Log "Invoking Start-EditorServicesHost"
    $editorServicesHost =
        Start-EditorServicesHost `
            -HostName $HostName `
            -HostProfileId $HostProfileId `
            -HostVersion $HostVersion `
            -LogPath $LogPath `
            -LogLevel $LogLevel `
            -AdditionalModules $AdditionalModules `
            -LanguageServiceNamedPipe $LanguageServicePipeName `
            -DebugServiceNamedPipe $DebugServicePipeName `
            -Stdio:($TransportType -eq "Stdio")`
            -BundledModulesPath $BundledModulesPath `
            -EnableConsoleRepl:$EnableConsoleRepl.IsPresent `
            -DebugServiceOnly:$DebugServiceOnly.IsPresent `
            -WaitForDebugger:$WaitForDebugger.IsPresent

    # TODO: Verify that the service is started
    Log "Start-EditorServicesHost returned $editorServicesHost"

    $resultDetails = @{
        "status" = "started";
        "languageServiceTransport" = $languageServiceTransport;
        "debugServiceTransport" = $debugServiceTransport;
    };

    if ($LanguageServicePipeName) {
        $resultDetails["languageServicePipeName"] = Get-NamedPipePath -PipeName $LanguageServicePipeName
        if ($IsLinux -or $IsMacOS) {
            Set-NamedPipeMode -PipeFile $resultDetails["languageServicePipeName"]
        }
    }
    if ($DebugServicePipeName) {
        $resultDetails["debugServicePipeName"] = Get-NamedPipePath -PipeName $DebugServicePipeName
        if ($IsLinux -or $IsMacOS) {
            Set-NamedPipeMode -PipeFile $resultDetails["debugServicePipeName"]
        }
    }

    # Notify the client that the services have started
    WriteSessionFile $resultDetails

    Log "Wrote out session file"
}
catch [System.Exception] {
    $e = $_.Exception;
    $errorString = ""

    Log "ERRORS caught starting up EditorServicesHost"

    while ($e -ne $null) {
        $errorString = $errorString + ($e.Message + "`r`n" + $e.StackTrace + "`r`n")
        $e = $e.InnerException;
        Log $errorString
    }

    ExitWithError ("An error occurred while starting PowerShell Editor Services:`r`n`r`n" + $errorString)
}

try {
    # Wait for the host to complete execution before exiting
    LogSection "Waiting for EditorServicesHost to complete execution"
    $editorServicesHost.WaitForCompletion()
    Log "EditorServicesHost has completed execution"
}
catch [System.Exception] {
    $e = $_.Exception;
    $errorString = ""

    Log "ERRORS caught while waiting for EditorServicesHost to complete execution"

    while ($e -ne $null) {
        $errorString = $errorString + ($e.Message + "`r`n" + $e.StackTrace + "`r`n")
        $e = $e.InnerException;
        Log $errorString
    }
}
