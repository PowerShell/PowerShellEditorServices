#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Microsoft.PowerShell.Management\Get-Item function:Clear-Host | Microsoft.PowerShell.Management\Set-Item function:__clearhost

function Clear-Host {
    [Alias('cls')]
    param(
        [Parameter()]
        [switch]
        $All
    )

    __clearhost
    if ($All.IsPresent) {
        $psEditor.Window.Terminal.Clear()
    }
}

if (!$IsMacOS -and !$IsLinux) {
    Set-Alias clear Clear-Host
}
