Import-LocalizedData -BindingVariable Strings -FileName Strings

Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 | ForEach-Object {
    . $PSItem.FullName
}
