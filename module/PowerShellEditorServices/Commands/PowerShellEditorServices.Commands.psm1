#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Import-LocalizedData -BindingVariable Strings -FileName Strings -ErrorAction Ignore

Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 | ForEach-Object {
    . $PSItem.FullName
}

Export-ModuleMember -Function *-*