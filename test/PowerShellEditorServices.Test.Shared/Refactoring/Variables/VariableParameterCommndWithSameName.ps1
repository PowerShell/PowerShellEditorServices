function Test-AADConnected {

    param (
        [Parameter(Mandatory = $false)][Alias("UPName")][String]$UserPrincipalName
    )
    Begin {}
    Process {
        [HashTable]$ConnectAADSplat = @{}
        if ($UserPrincipalName) {
            $ConnectAADSplat = @{
                AccountId   = $UserPrincipalName
                ErrorAction = 'Stop'
            }
        }
    }
}

function Set-MSolUMFA{
    [CmdletBinding(SupportsShouldProcess=$true)]
    param (
        [Parameter(Mandatory=$true,ValueFromPipelineByPropertyName=$true)][string]$UserPrincipalName,
        [Parameter(Mandatory=$true,ValueFromPipelineByPropertyName=$true)][ValidateSet('Enabled','Disabled','Enforced')][String]$StrongAuthenticationRequiremets
    )
    begin{
        # Check if connected to Msol Session already
        if (!(Test-MSolConnected)) {
            Write-Verbose('No existing Msol session detected')
            try {
                Write-Verbose('Initiating connection to Msol')
                Connect-MsolService -ErrorAction Stop
                Write-Verbose('Connected to Msol successfully')
            }catch{
                return Write-Error($_.Exception.Message)
            }
        }
        if(!(Get-MsolUser -MaxResults 1 -ErrorAction Stop)){
            return Write-Error('Insufficient permissions to set MFA')
        }
    }
    Process{
        # Get the time and calc 2 min to the future
        $TimeStart = Get-Date
        $TimeEnd = $timeStart.addminutes(1)
        $Finished=$false
        #Loop to check if the user exists already
        if ($PSCmdlet.ShouldProcess($UserPrincipalName, "StrongAuthenticationRequiremets = "+$StrongAuthenticationRequiremets)) {
        }
    }
    End{}
}

Set-MsolUser -UserPrincipalName $UPN -StrongAuthenticationRequirements $sta -ErrorAction Stop
$UserPrincipalName = "Bob"
if ($UserPrincipalName) {
    $SplatTestAADConnected.Add('UserPrincipalName', $UserPrincipalName)
}
