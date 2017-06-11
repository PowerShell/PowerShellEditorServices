Import-LocalizedData -BindingVariable Strings -FileName Strings -ErrorAction Ignore

Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 | ForEach-Object {
    . $PSItem.FullName
}

Export-ModuleMember -Function *-*