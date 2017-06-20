#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

if (!$PSVersionTable.PSEdition -or $PSVersionTable.PSEdition -eq "Desktop") {
    Add-Type -Path "$PSScriptRoot/bin/Desktop/Microsoft.PowerShell.EditorServices.VSCode.dll"
}
else {
    Add-Type -Path "$PSScriptRoot/bin/Core/Microsoft.PowerShell.EditorServices.VSCode.dll"
}

if ($psEditor -is [Microsoft.PowerShell.EditorServices.Extensions.EditorObject]) {
    [Microsoft.PowerShell.EditorServices.VSCode.ComponentRegistration]::Register($psEditor.Components)
}
else {
    Write-Verbose '$psEditor object not found in the session, components will not be registered.'
}

Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 -Recurse | ForEach-Object {
    . $PSItem.FullName
}
