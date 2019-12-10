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

    [ValidateSet("Diagnostic", "Verbose", "Normal", "Warning", "Error")]
    $LogLevel,

	[Parameter(Mandatory=$true)]
	[ValidateNotNullOrEmpty()]
	[string]
	$SessionDetailsPath,

    [switch]
    $EnableConsoleRepl,

    [switch]
    $UseLegacyReadLine,

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

Import-Module -Name "$PSScriptRoot/PowerShellEditorServices.psd1"
Start-EditorServices @PSBoundParameters
