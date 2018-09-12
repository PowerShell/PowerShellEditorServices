#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Microsoft.PowerShell.Utility\Add-Type -Path "$PSScriptRoot/bin/Microsoft.PowerShell.EditorServices.VSCode.dll"

if ($psEditor -is [Microsoft.PowerShell.EditorServices.Extensions.EditorObject]) {
    [Microsoft.PowerShell.EditorServices.VSCode.ComponentRegistration]::Register($psEditor.Components)
}
else {
    Write-Verbose '$psEditor object not found in the session, components will not be registered.'
}

Microsoft.PowerShell.Management\Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 -Recurse | ForEach-Object {
    . $PSItem.FullName
}
