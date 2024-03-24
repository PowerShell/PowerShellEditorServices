function New-User {
    param (
        [string][Alias("Username")]$Renamed,
        [string]$password
    )
    write-host $Renamed + $password

    $splat= @{
        Renamed = "JohnDeer"
        Password = "SomePassword"
    }
    New-User @splat
}

$UserDetailsSplat= @{
    Renamed = "JohnDoe"
    Password = "SomePassword"
}
New-User @UserDetailsSplat

New-User -Renamed "JohnDoe" -Password "SomePassword"
