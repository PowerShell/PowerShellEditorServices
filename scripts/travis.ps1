Get-Module PowerShellGet,PackageManagement | Remove-Module -Force -Verbose
powershell -Command { Install-Module -Name PowershellGet -MinimumVersion 1.6 -Scope CurrentUser -force -confirm:$false -verbose }
powershell -Command { Install-Module -Name PackageManagement -MinimumVersion 1.1.7.0 -Scope CurrentUser -Force -Confirm:$false -Verbose }
Import-Module -Name PowerShellGet -MinimumVersion 1.6 -Force
Import-Module -Name PackageManagement -MinimumVersion 1.1.7.0 -Force
Install-PackageProvider -Name NuGet -Force | Out-Null
Import-PackageProvider NuGet -Force | Out-Null
Set-PSRepository -Name PSGallery -InstallationPolicy Trusted | Out-Null

# Install InvokeBuild
Install-Module InvokeBuild -MaximumVersion 5.1.0 -Scope CurrentUser -Force
Install-Module PlatyPS -RequiredVersion 0.9.0 -Scope CurrentUser -Force


# Build the code and perform tests
Import-module InvokeBuild
Invoke-Build -Configuration Release
