# PowerShell Editor Services Bootstrapper Script
# ----------------------------------------------
# This script contains startup logic for the PowerShell Editor Services
# module when launched by an editor.  It handles the following tasks:
#
# - Verifying the existence of dependencies like PowerShellGet
# - Verifying that the expected version of the PowerShellEditorServices module is installed
# - Installing the PowerShellEditorServices module if confirmed by the user
# - Finding unused TCP port numbers for the language and debug services to use
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

    [ValidateSet("Diagnostic", "Normal", "Verbose", "Error", "Diagnostic")]
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

$minPortNumber = 10000
$maxPortNumber = 30000

if ($LogLevel -eq "Diagnostic") {
    $VerbosePreference = 'Continue'
    $scriptName = [System.IO.Path]::GetFileNameWithoutExtension($MyInvocation.MyCommand.Name)
    $logFileName = [System.IO.Path]::GetFileName($LogPath)
    Start-Transcript (Join-Path (Split-Path $LogPath -Parent) "$scriptName-$logFileName") -Force
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

function Test-PortAvailability {
    param(
        [Parameter(Mandatory=$true)]
        [int]
        $PortNumber
    )

    $portAvailable = $true

    try {
        # After some research, I don't believe we should run into problems using an IPv4 port
        # that happens to be in use via an IPv6 address.  That is based on this info:
        # https://www.ibm.com/support/knowledgecenter/ssw_i5_54/rzai2/rzai2compipv4ipv6.htm#rzai2compipv4ipv6__compports
        $ipAddress = [System.Net.IPAddress]::Loopback
        Log "Testing availability of port ${PortNumber} at address ${ipAddress} / $($ipAddress.AddressFamily)"

        $tcpListener = Microsoft.PowerShell.Utility\New-Object System.Net.Sockets.TcpListener @($ipAddress, $PortNumber)
        $tcpListener.Start()
        $tcpListener.Stop()
    }
    catch [System.Net.Sockets.SocketException] {
        $portAvailable = $false

        # Check the SocketErrorCode to see if it's the expected exception
        if ($_.Exception.SocketErrorCode -eq [System.Net.Sockets.SocketError]::AddressAlreadyInUse) {
            Log "Port $PortNumber is in use."
        }
        else {
            Log "SocketException on port ${PortNumber}: $($_.Exception)"
        }
    }

    $portAvailable
}

$portsInUse = @{}
$rand = Microsoft.PowerShell.Utility\New-Object System.Random
function Get-AvailablePort() {
    $triesRemaining = 10;

    while ($triesRemaining -gt 0) {
        do {
            $port = $rand.Next($minPortNumber, $maxPortNumber)
        }
        while ($portsInUse.ContainsKey($port))

        # Whether we succeed or fail, don't try this port again
        $portsInUse[$port] = 1

        Log "Checking port: $port, attempts remaining $triesRemaining --------------------"
        if ((Test-PortAvailability -PortNumber $port) -eq $true) {
            Log "Port: $port is available"
            return $port
        }

        Log "Port: $port is NOT available"
        $triesRemaining--;
    }

    Log "Did not find any available ports!!"
    return $null
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

	if ($Stdio.IsPresent -and -not $DebugServiceOnly.IsPresent) { $languageServiceTransport = "Stdio" }
	elseif ($LanguageServicePipeName)                           { $languageServiceTransport = "NamedPipe"; $languageServicePipeName = "$LanguageServicePipeName" }
	elseif ($languageServicePort = Get-AvailablePort)           { $languageServiceTransport = "Tcp" }
	else                                                        { ExitWithError "Failed to find an open socket port for language service." }

	if ($Stdio.IsPresent -and $DebugServiceOnly.IsPresent)      { $debugServiceTransport = "Stdio" }
	elseif ($DebugServicePipeName)                              { $debugServiceTransport = "NamedPipe"; $debugServicePipeName = "$DebugServicePipeName" }
	elseif ($debugServicePort = Get-AvailablePort)              { $debugServiceTransport = "Tcp" }
	else                                                        { ExitWithError "Failed to find an open socket port for debug service." }

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
            -LanguageServicePort $languageServicePort `
            -DebugServicePort $debugServicePort `
            -LanguageServiceNamedPipe $LanguageServicePipeName `
            -DebugServiceNamedPipe $DebugServicePipeName `
            -Stdio:$Stdio.IsPresent`
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

    if ($languageServicePipeName) { $resultDetails["languageServicePipeName"] = "$languageServicePipeName" }
    if ($debugServicePipeName)    { $resultDetails["debugServicePipeName"]    = "$debugServicePipeName" }

    if ($languageServicePort)     { $resultDetails["languageServicePort"]     = $languageServicePort }
    if ($debugServicePort)        { $resultDetails["debugServicePort"]        = $debugServicePort }

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
