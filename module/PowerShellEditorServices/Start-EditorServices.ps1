<#
.SYNOPSIS
    Starts the language and debug services from the PowerShellEditorServices module.
.DESCRIPTION
    PowerShell Editor Services Bootstrapper Script
    ----------------------------------------------
    This script contains startup logic for the PowerShell Editor Services
    module when launched by an editor.  It handles the following tasks:

    - Verifying the existence of dependencies like PowerShellGet
    - Verifying that the expected version of the PowerShellEditorServices module is installed
    - Installing the PowerShellEditorServices module if confirmed by the user
    - Creating named pipes for the language and debug services to use (if using named pipes)
    - Starting the language and debug services from the PowerShellEditorServices module
.INPUTS
    None
.OUTPUTS
    None
.NOTES
    If editor integration authors make modifications to this script, please
    consider contributing changes back to the canonical version of this script
    at the PowerShell Editor Services GitHub repository:
    https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/PowerShellEditorServices/Start-EditorServices.ps1'
#>
[CmdletBinding(DefaultParameterSetName="NamedPipe")]
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

    [Parameter(ParameterSetName="Stdio", Mandatory=$true)]
    [switch]
    $Stdio,

    [Parameter(ParameterSetName="NamedPipe")]
    [string]
    $LanguageServicePipeName = $null,

    [Parameter(ParameterSetName="NamedPipe")]
    [string]
    $DebugServicePipeName = $null,

    [Parameter(ParameterSetName="NamedPipeSimplex")]
    [switch]
    $SplitInOutPipes,

    [Parameter(ParameterSetName="NamedPipeSimplex")]
    [string]
    $LanguageServiceInPipeName,

    [Parameter(ParameterSetName="NamedPipeSimplex")]
    [string]
    $LanguageServiceOutPipeName,

    [Parameter(ParameterSetName="NamedPipeSimplex")]
    [string]
    $DebugServiceInPipeName = $null,

    [Parameter(ParameterSetName="NamedPipeSimplex")]
    [string]
    $DebugServiceOutPipeName = $null
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

# net45 is not supported, only net451 and up
if ($PSVersionTable.PSVersion.Major -le 5) {
    $net451Version = 378675
    $dotnetVersion = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\").Release
    if ($dotnetVersion -lt $net451Version) {
        Write-SessionFile @{
            status = failed
            reason = "netversion"
            detail = "$netVersion"
        }

        ExitWithError "Your .NET version is too low. Upgrade to net451 or higher to run the PowerShell extension."
    }
}

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
    if ($null -ne $modules) {
        if ($null -ne $ModuleVersion) {
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
        $PipeName = "PSES_$([System.IO.Path]::GetRandomFileName())"

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

    if (($PSVersionTable.PSVersion.Major -le 5) -or $IsWindows) {
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
    return !(Test-Path $path)
}

function Set-NamedPipeMode {
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $PipeFile
    )

    if (($PSVersionTable.PSVersion.Major -le 5) -or $IsWindows) {
        return
    }

    chmod $DEFAULT_USER_MODE $PipeFile

    if ($IsLinux) {
        $mode = /usr/bin/stat -c "%a" $PipeFile
    }
    elseif ($IsMacOS) {
        $mode = /usr/bin/stat -f "%A" $PipeFile
    }

    if ($mode -ne $DEFAULT_USER_MODE) {
        ExitWithError "Permissions to the pipe file were not set properly. Expected: $DEFAULT_USER_MODE Actual: $mode for file: $PipeFile"
    }
}

LogSection "Console Encoding"
Log $OutputEncoding

function Get-ValidatedNamedPipeName {
    param(
        [string]
        $PipeName
    )

    # If no PipeName is passed in, then we create one that's guaranteed to be valid
    if (!$PipeName) {
        $PipeName = New-NamedPipeName
    }
    elseif (!(Test-NamedPipeName -PipeName $PipeName)) {
        ExitWithError "Pipe name supplied is already in use: $PipeName"
    }

    return $PipeName
}

function Set-PipeFileResult {
    param (
        [Hashtable]
        $ResultTable,

        [string]
        $PipeNameKey,

        [string]
        $PipeNameValue
    )

    $ResultTable[$PipeNameKey] = Get-NamedPipePath -PipeName $PipeNameValue
    if (($PSVersionTable.PSVersion.Major -ge 6) -and ($IsLinux -or $IsMacOS)) {
        Set-NamedPipeMode -PipeFile $ResultTable[$PipeNameKey]
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

    if ($EnableConsoleRepl) {
        Write-Host "PowerShell Integrated Console`n"
    }

    $resultDetails = @{
        "status" = "not started";
        "languageServiceTransport" = $PSCmdlet.ParameterSetName;
        "debugServiceTransport" = $PSCmdlet.ParameterSetName;
    };

    # Create the Editor Services host
    Log "Invoking Start-EditorServicesHost"

    $splat = @{
        HostName = $HostName
        HostProfileId = $HostProfileId
        HostVersion = $HostVersion
        LogPath = $LogPath
        LogLevel = $LogLevel
        AdditionalModules = $AdditionalModules
        BundledModulesPath = $BundledModulesPath
        EnableConsoleRepl = $EnableConsoleRepl.IsPresent
        DebugServiceOnly = $DebugServiceOnly.IsPresent
        WaitForDebugger = $WaitForDebugger.IsPresent
        FeatureFlags = $FeatureFlags
    }

    # There could be only one service on Stdio channel
    # Locate available port numbers for services
    switch ($PSCmdlet.ParameterSetName) {
        "Stdio" {
            $splat.Stdio = $true
            $editorServicesHost = Start-EditorServicesHost @splat
            break
        }

        "NamedPipeSimplex" {
            $splat.LanguageServiceInNamedPipe = Get-ValidatedNamedPipeName $LanguageServiceInPipeName
            $splat.LanguageServiceOutNamedPipe = Get-ValidatedNamedPipeName $LanguageServiceOutPipeName
            $splat.DebugServiceInNamedPipe = Get-ValidatedNamedPipeName $DebugServiceInPipeName
            $splat.DebugServiceOutNamedPipe = Get-ValidatedNamedPipeName $DebugServiceOutPipeName

            $editorServicesHost = Start-EditorServicesHost @splat

            Set-PipeFileResult $resultDetails "languageServiceReadPipeName" $splat.LanguageServiceInNamedPipe
            Set-PipeFileResult $resultDetails "languageServiceWritePipeName" $splat.LanguageServiceOutNamedPipe
            Set-PipeFileResult $resultDetails "debugServiceReadPipeName" $splat.DebugServiceInNamedPipe
            Set-PipeFileResult $resultDetails "debugServiceWritePipeName" $splat.DebugServiceOutNamedPipe
            break
        }

        Default {
            $splat.LanguageServiceNamedPipe = Get-ValidatedNamedPipeName $LanguageServicePipeName
            $splat.DebugServiceNamedPipe = Get-ValidatedNamedPipeName $DebugServicePipeName

            $editorServicesHost = Start-EditorServicesHost @splat
            Log ($splat | Out-String)
            Log $LanguageServicePipeName
            Set-PipeFileResult $resultDetails "languageServicePipeName" $splat.LanguageServiceNamedPipe
            Set-PipeFileResult $resultDetails "debugServicePipeName" $splat.DebugServiceNamedPipe
            break
        }
    }

    # TODO: Verify that the service is started
    Log "Start-EditorServicesHost returned $editorServicesHost"

    $resultDetails["status"] = "started"

    # Notify the client that the services have started
    WriteSessionFile $resultDetails

    Log "Wrote out session file"
}
catch [System.Exception] {
    $e = $_.Exception;
    $errorString = ""

    Log "ERRORS caught starting up EditorServicesHost"

    while ($null -ne $e) {
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

    while ($null -ne $e) {
        $errorString = $errorString + ($e.Message + "`r`n" + $e.StackTrace + "`r`n")
        $e = $e.InnerException;
        Log $errorString
    }
}
