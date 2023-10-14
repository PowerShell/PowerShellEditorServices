function Test-AADConnected {

    param (
        [Parameter(Mandatory = $false)][String]$Renamed
    )
    Begin {}
    Process {
        [HashTable]$ConnectAADSplat = @{}
        if ($Renamed) {
            $ConnectAADSplat = @{
                AccountId   = $Renamed
                ErrorAction = 'Stop'
            }
        }
    }
}

Set-MsolUser -UserPrincipalName $UPN -StrongAuthenticationRequirements $sta -ErrorAction Stop
$UserPrincipalName = "Bob"
if ($UserPrincipalName) {
    $SplatTestAADConnected.Add('UserPrincipalName', $UserPrincipalName)
}
