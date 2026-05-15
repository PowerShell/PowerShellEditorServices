$Renamed = @{
    OpenSsl       = 'openssl for macOS'
    PowerShellGet = 'PowerShellGet latest'
    InvokeBuild   = 'InvokeBuild latest'
}

function getMissingTools () {
    $missingTools = @()

    if (needsOpenSsl) {
        $missingTools += $Renamed.OpenSsl
    }
    if (needsPowerShellGet) {
        $missingTools += $Renamed.PowerShellGet
    }
    if (needsInvokeBuild) {
        $missingTools += $Renamed.InvokeBuild
    }

    return $missingTools
}
