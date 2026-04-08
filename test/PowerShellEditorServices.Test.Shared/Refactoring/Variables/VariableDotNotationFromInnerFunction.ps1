$NeededTools = @{
    OpenSsl       = 'openssl for macOS'
    PowerShellGet = 'PowerShellGet latest'
    InvokeBuild   = 'InvokeBuild latest'
}

function getMissingTools () {
    $missingTools = @()

    if (needsOpenSsl) {
        $missingTools += $NeededTools.OpenSsl
    }
    if (needsPowerShellGet) {
        $missingTools += $NeededTools.PowerShellGet
    }
    if (needsInvokeBuild) {
        $missingTools += $NeededTools.InvokeBuild
    }

    return $missingTools
}
