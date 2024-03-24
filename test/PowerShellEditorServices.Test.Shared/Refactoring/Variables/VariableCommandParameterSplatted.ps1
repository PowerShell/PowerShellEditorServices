function New-User {
    param (
        [string]$Username,
        [string]$password
    )
    write-host $username + $password

    $splat= @{
        Username = "JohnDeer"
        Password = "SomePassword"
    }
    New-User @splat
}

$UserDetailsSplat= @{
    Username = "JohnDoe"
    Password = "SomePassword"
}
New-User @UserDetailsSplat

New-User -Username "JohnDoe" -Password "SomePassword"
