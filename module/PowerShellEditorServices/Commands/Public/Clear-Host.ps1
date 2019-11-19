#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function Clear-Host {
    [Alias('cls')]
    param()

    [System.Console]::Clear()
    $psEditor.Window.Terminal.Clear()
}
