# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

Microsoft.PowerShell.Utility\Import-LocalizedData -BindingVariable Strings -FileName Strings -ErrorAction Ignore

Microsoft.PowerShell.Management\Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 | ForEach-Object {
    . $PSItem.FullName
}

Microsoft.PowerShell.Core\Export-ModuleMember -Function *-*
