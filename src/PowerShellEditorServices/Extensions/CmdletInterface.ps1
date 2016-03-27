<#
 .SYNOPSIS
 Registers a command which can be executed in the host editor.

 .DESCRIPTION
 Registers a command which can be executed in the host editor.  This
 command will be shown to the user either in a menu or command palette.
 Upon invoking this command, either a function/cmdlet or ScriptBlock will
 be executed depending on whether the -Function or -ScriptBlock parameter
 was used when the command was registered.

 This command can be run multiple times for the same command so that its
 details can be updated.  However, re-registration of commands should only
 be used for development purposes, not for dynamic behavior.

 .PARAMETER Name
 Specifies a unique name which can be used to identify this command.
 This name is not displayed to the user.

 .PARAMETER DisplayName
 Specifies a display name which is displayed to the user.

 .PARAMETER Function
 Specifies a function or cmdlet name which will be executed when the user
 invokes this command.  This function may take a parameter called $context
 which will be populated with an EditorContext object containing information
 about the host editor's state at the time the command was executed.

 .PARAMETER ScriptBlock
 Specifies a ScriptBlock which will be executed when the user invokes this
 command.  This ScriptBlock may take a parameter called $context
 which will be populated with an EditorContext object containing information
 about the host editor's state at the time the command was executed.

 .PARAMETER SuppressOutput
 If provided, causes the output of the editor command to be suppressed when
 it is run.  Errors that occur while running this command will still be
 written to the host.

 .EXAMPLE
 PS> Register-EditorCommand -Name "MyModule.MyFunctionCommand" -DisplayName "My function command" -Function Invoke-MyCommand -SuppressOutput

 .EXAMPLE
 PS> Register-EditorCommand -Name "MyModule.MyScriptBlockCommand" -DisplayName "My ScriptBlock command" -ScriptBlock { Write-Output "Hello from my command!" }

 .LINK
 Unregister-EditorCommand
#>
function Register-EditorCommand {
	[CmdletBinding()]
	param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$DisplayName,

        [Parameter(
            Mandatory=$true,
            ParameterSetName="Function")]
        [ValidateNotNullOrEmpty()]
        [string]$Function,

        [Parameter(
            Mandatory=$true,
            ParameterSetName="ScriptBlock")]
        [ValidateNotNullOrEmpty()]
        [ScriptBlock]$ScriptBlock,

		[switch]$SuppressOutput
	)

    Process
    {
		$commandArgs = @($Name, $DisplayName, $SuppressOutput.IsPresent)

		if ($ScriptBlock -ne $null)
		{
			Write-Verbose "Registering command '$Name' which executes a ScriptBlock"
			$commandArgs += $ScriptBlock
		}
		else
		{
			Write-Verbose "Registering command '$Name' which executes a function"
			$commandArgs += $Function
		}

		$editorCommand = New-Object Microsoft.PowerShell.EditorServices.Extensions.EditorCommand -ArgumentList $commandArgs
		$psEditor.RegisterCommand($editorCommand)
    }
}

<#
 .SYNOPSIS
 Unregisters a command which has already been registered in the host editor.

 .DESCRIPTION
 Unregisters a command which has already been registered in the host editor.
 An error will be thrown if the specified Name is unknown.

 .PARAMETER Name
 Specifies a unique name which identifies a command which has already been registered.

 .EXAMPLE
 PS> Unregister-EditorCommand -Name "MyModule.MyFunctionCommand"

 .LINK
 Register-EditorCommand
#>
function Unregister-EditorCommand {
	[CmdletBinding()]
	param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]$Name
	)

    Process
    {
        Write-Verbose "Unregistering command '$Name'"
		$psEditor.UnregisterCommand($Name);
    }
}

function psedit {
	param([Parameter(Mandatory=$true)]$FilePaths)

	dir $FilePaths | where { !$_.PSIsContainer } | % {
		$psEditor.Workspace.OpenFile($_.FullName)
	}
}
