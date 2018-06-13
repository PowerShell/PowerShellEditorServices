#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Microsoft.PowerShell.Utility\Import-LocalizedData -BindingVariable Strings -FileName Strings -ErrorAction Ignore

Microsoft.PowerShell.Management\Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 | ForEach-Object {
    . $PSItem.FullName
}

Microsoft.PowerShell.Core\Export-ModuleMember -Function *-*
