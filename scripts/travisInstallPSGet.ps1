$ErrorActionPreference = 'Stop'

# Installs PowerShellGet so that the main travis script will work properly

Get-Module PowerShellGet,PackageManagement | Remove-Module -Force -Verbose
powershell -Command { Install-Module -Name PowershellGet -MinimumVersion 1.6 -Scope CurrentUser -force -confirm:$false -verbose }
powershell -Command { Install-Module -Name PackageManagement -MinimumVersion 1.1.7.0 -Scope CurrentUser -Force -Confirm:$false -Verbose }
Import-Module -Name PowerShellGet -MinimumVersion 1.6 -Force
Import-Module -Name PackageManagement -MinimumVersion 1.1.7.0 -Force
Install-PackageProvider -Name NuGet -Force -Verbose
