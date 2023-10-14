function Test-AADConnected {

    param (
        [Parameter(Mandatory = $false)][String]$UserPrincipalName
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

Set-MsolUser -UserPrincipalName $UPN -StrongAuthenticationRequirements $sta -ErrorAction Stop
$UserPrincipalName = "Bob"
if ($UserPrincipalName) {
    $SplatTestAADConnected.Add('UserPrincipalName', $UserPrincipalName)
}
