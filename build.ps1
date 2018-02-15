#!/usr/bin/env pwsh
param(
    [Parameter()]
    [switch]
    $Bootstrap,

    [Parameter()]
    [switch]
    $Clean,

    [Parameter()]
    [switch]
    $Test
)

$NeededTools = @{
    OpenSsl = "openssl for macOS"
    PowerShellGet = "PowerShellGet latest"
    InvokeBuild = "InvokeBuild latest"
}

if ((-not $PSVersionTable["OS"]) -or $PSVersionTable["OS"].Contains("Windows")) {
    $OS = "Windows"
} elseif ($PSVersionTable["OS"].Contains("Darwin")) {
    $OS = "macOS"
} else {
    $OS = "Linux"
}


function needsOpenSsl () {
    if ($OS -eq "macOS") {
        try {
            $opensslVersion = (openssl version)
        } catch {
            return $true
        }
    }
    return $false
}

function needsPowerShellGet () {
    if (Get-Module -ListAvailable -Name PowerShellGet) {
        return $false
    }
    return $true
}

function needsInvokeBuild () {
    if (Get-Module -ListAvailable -Name InvokeBuild) {
        return $false
    }
    return $true
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

function hasMissingTools () {
    return ((getMissingTools).Count -gt 0)
}

if ($Bootstrap) {
    $string = "Here is what your environment is missing:`n"
    $missingTools = getMissingTools
    if (($missingTools).Count -eq 0) {
        $string += "* nothing!`n`n Run this script without a flag to build or a -Clean to clean."
    } else {
        $missingTools | ForEach-Object {$string += "* $_`n"}
        $string += "`nAll instructions for installing these tools can be found on PowerShell Editor Services' Github:`n" `
            + "https://github.com/powershell/PowerShellEditorServices#development"
    }
    Write-Host "`n$string`n"
} elseif(hasMissingTools) {
    Write-Host "You are missing needed tools. Run './build.ps1 -Bootstrap' to see what they are."
} else {
    if($Clean) {
        Invoke-Build Clean
    }

    Invoke-Build Build

    if($Test) {
        Invoke-Build Test
    }
}