function New-User {
    param (
        [string][Alias("Username")]$Renamed,
        [string]$password
    )
    write-host $Renamed + $password
}

$UserDetailsSplat= @{
    Renamed = "JohnDoe"
    Password = "SomePassword"
}
New-User @UserDetailsSplat

New-User -Renamed "JohnDoe" -Password "SomePassword"
