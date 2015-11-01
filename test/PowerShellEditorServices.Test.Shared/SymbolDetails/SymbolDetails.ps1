Get-Process -ComputerName "test-computer"

<#
.Synopsis
	Short description
.DESCRIPTION
	Long description
.EXAMPLE
	Example of how to use this cmdlet
.EXAMPLE
	Another example of how to use this cmdlet
#>
function Get-Thing
{
	[Alias()]
	[OutputType([int])]
	Param
	(
		# Param1 help description
		[Parameter(Mandatory=$true,
				   ValueFromPipelineByPropertyName=$true,
				   Position=0)]
		$Name
	)
	
	Begin
	{
	}
	Process
	{
		return 0;
	}
	End
	{
	}
}

Get-Thing -Name "test"
