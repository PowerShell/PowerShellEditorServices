param(
    [ValidateSet("Debug", "Release")]
    $Configuration = "Debug"
)

#Requires -Modules @{ModuleName="InvokeBuild";ModuleVersion="3.2.1"}

task EnsureDotNet -Before Clean, Build, BuildHost, Test, TestPowerShellApi {
    # TODO: Download if it doesn't exist in known paths
    if (!(Test-Path 'c:\Program Files\dotnet\dotnet.exe')) {
        Write-Error "dotnet is not installed"
    }

    exec {
        if (!((& dotnet --version) -like "*preview4*")) {
            Write-Error "You must have at least preview4 of the dotnet tools installed."
        }
    }
}

task Clean {
    exec { & dotnet clean .\PowerShellEditorServices.sln }
}

function BuildForPowerShellVersion($version) {
    exec { & dotnet restore .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -- /p:PowerShellVersion=$version }

    Write-Host -ForegroundColor Green "`n### Testing API usage for PowerShell $version...`n"
    exec { & dotnet build -f net451 .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -- /p:PowerShellVersion=$version}
}

task TestPowerShellApi {
    BuildForPowerShellVersion v3
    BuildForPowerShellVersion v4
    BuildForPowerShellVersion v5r1
    BuildForPowerShellVersion v5r2
}

task BuildHost EnsureDotNet, {
    # This task is meant to be used in a quick dev cycle so no 'restore' is done first
    exec { & dotnet build -c $Configuration .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj }
}

task Build {
    exec { & dotnet restore -v:m .\PowerShellEditorServices.sln }
    exec { & dotnet build -c $Configuration .\PowerShellEditorServices.sln }
}

task Test {
    $testParams = @{}
    if ($env:APPVEYOR -ne $null) {
        $testParams = @{"l" = "appveyor"}
    }

    exec { & dotnet test -c $Configuration @testParams .\test\PowerShellEditorServices.Test\PowerShellEditorServices.Test.csproj }
    exec { & dotnet test -c $Configuration @testParams .\test\PowerShellEditorServices.Test.Protocol\PowerShellEditorServices.Test.Protocol.csproj }
    exec { & dotnet test -c $Configuration @testParams .\test\PowerShellEditorServices.Test.Host\PowerShellEditorServices.Test.Host.csproj }
}

task LayoutModule -After Build, BuildHost {
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\ -Type Directory | Out-Null
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop -Type Directory | Out-Null
    New-Item -Force $PSScriptRoot\module\PowerShellEditorServices\bin\Core -Type Directory | Out-Null

    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\net451\* -Filter Microsoft.PowerShell.EditorServices*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\net451\Newtonsoft.Json.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Desktop\
    Copy-Item -Force -Path $PSScriptRoot\src\PowerShellEditorServices.Host\bin\$Configuration\netstandard1.6\* -Filter Microsoft.PowerShell.EditorServices*.dll -Destination $PSScriptRoot\module\PowerShellEditorServices\bin\Core\
}

task . Clean, Build, Test, TestPowerShellApi
