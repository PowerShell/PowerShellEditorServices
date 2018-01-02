

# Install InvokeBuild
Install-Module InvokeBuild -MaximumVersion 5.1.0 -Scope CurrentUser -Force
Install-Module PlatyPS -MaximumVersion 0.10.0 -Scope CurrentUser -Force


# Build the code and perform tests
Import-module InvokeBuild
Invoke-Build -Configuration Release
