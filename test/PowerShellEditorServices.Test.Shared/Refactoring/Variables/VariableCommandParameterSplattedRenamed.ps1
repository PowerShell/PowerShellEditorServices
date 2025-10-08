function New-User {
    param (
        [string]$Renamed,
        [string]$password
    )
    Write-Host $Renamed + $password

    $splat = @{
        Renamed = 'JohnDeer'
        Password = 'SomePassword'
    }
    New-User @splat
}

$UserDetailsSplat = @{
    Renamed = 'JohnDoe'
    Password = 'SomePassword'
}
New-User @UserDetailsSplat

New-User -Renamed 'JohnDoe' -Password 'SomePassword'
