function New-User {
    param (
        [string]$Username,
        [string]$password
    )
    write-host $username + $password
}

$UserDetailsSplat= @{
    Username = "JohnDoe"
    Password = "SomePassword"
}
New-User @UserDetailsSplat

New-User -Username "JohnDoe" -Password "SomePassword"
