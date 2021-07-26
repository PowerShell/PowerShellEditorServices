# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$ErrorActionPreference = 'Stop'

Set-PSRepository -Name PSGallery -InstallationPolicy Trusted | Out-Null
if ($IsWindows -or $PSVersionTable.PSVersion.Major -lt 6) {
    # We rely on PowerShellGet's -AllowPrerelease which is in PowerShellGet 1.6 so we need to update PowerShellGet.
    Get-Module PowerShellGet,PackageManagement | Remove-Module -Force
    powershell -Command { Install-Module -Name PowerShellGet -MinimumVersion 1.6 -Force }
    powershell -Command { Install-Module -Name PackageManagement -MinimumVersion 1.1.7.0 -Force }
    Import-Module -Name PowerShellGet -MinimumVersion 1.6 -Force
    Import-Module -Name PackageManagement -MinimumVersion 1.1.7.0 -Force
}

# Update help needed for SignatureHelp LSP request.
Update-Help -Force -ErrorAction SilentlyContinue

# Needed for build and docs gen.
Install-Module -Name InvokeBuild -MaximumVersion 5.1.0 -Scope CurrentUser -Force
Install-Module -Name PlatyPS -RequiredVersion 0.9.0 -Scope CurrentUser -Force

Invoke-Build -Configuration Release
