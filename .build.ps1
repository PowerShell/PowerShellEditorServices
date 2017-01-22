param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

#Requires -Modules @{ModuleName="InvokeBuild";ModuleVersion="3.2.1"}

$script:IsCIBuild = $env:APPVEYOR -ne $null

task SetupDotNet -Before Restore, Clean, Build, BuildHost, Test, TestPowerShellApi {

    # Bail out early if we've already found the exe path
    if ($script:dotnetExe -ne $null) { return }

    $requiredDotnetVersion = "1.0.0-preview4-004233"
    $needsInstall = $true
    $dotnetPath = "$PSScriptRoot/.dotnet"
    $dotnetExePath = "$dotnetPath/dotnet.exe"

    if (Test-Path $dotnetExePath) {
        $script:dotnetExe = $dotnetExePath
    }
    else {
        $installedDotnet = Get-Command dotnet -ErrorAction Ignore
        if ($installedDotnet) {
            $dotnetExePath = $installedDotnet.Source

            exec {
                if ((& $dotnetExePath --version) -eq $requiredDotnetVersion) {
                    $script:dotnetExe = $dotnetExePath
                }
            }
        }

        if ($script:dotnetExe -eq $null) {

            Write-Host "`n### Installing .NET CLI $requiredDotnetVersion...`n" -ForegroundColor Green

            # Download the official installation script and run it
            $installScriptPath = "$($env:TEMP)\dotnet-install.ps1"
            Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview4/scripts/obtain/dotnet-install.ps1" -OutFile $installScriptPath
            $env:DOTNET_INSTALL_DIR = "$PSScriptRoot\.dotnet"
            & $installScriptPath -Version $requiredDotnetVersion -InstallDir "$env:DOTNET_INSTALL_DIR"

            Write-Host "`n### Installation complete." -ForegroundColor Green
            $script:dotnetExe = $dotnetExePath
        }
    }

    # This variable is used internally by 'dotnet' to know where it's installed
    $script:dotnetExe = Resolve-Path $script:dotnetExe
    if (!$env:DOTNET_INSTALL_DIR)
    {
        $dotnetExeDir = [System.IO.Path]::GetDirectoryName($script:dotnetExe)
        $env:PATH = $dotnetExeDir + [System.IO.Path]::PathSeparator + $env:PATH
        $env:DOTNET_INSTALL_DIR = $dotnetExeDir
    }

    Write-Host "`n### Using dotnet at path $script:dotnetExe`n" -ForegroundColor Green
}

task Restore {
    exec { & dotnet restore }
}

task Clean {
    exec { & dotnet clean }
    Get-ChildItem -Recurse src\*.nupkg | Remove-Item -Force -ErrorAction Ignore
    Get-ChildItem module\*.zip | Remove-Item -Force -ErrorAction Ignore
}

task GetProductVersion -Before PackageNuGet, PackageModule, UploadArtifacts {
    if ($script:BaseVersion) { return }
    [xml]$props = Get-Content .\PowerShellEditorServices.Common.props

    $script:VersionSuffix = $props.Project.PropertyGroup.VersionSuffix
    $script:BaseVersion = "$($props.Project.PropertyGroup.VersionPrefix)-$($props.Project.PropertyGroup.VersionSuffix)"
    $script:FullVersion = "$($props.Project.PropertyGroup.VersionPrefix)-$($props.Project.PropertyGroup.VersionSuffix)"

    if ($env:APPVEYOR) {
        $script:BuildNumber = $env:APPVEYOR_BUILD_NUMBER
        $script:FullVersion = "$script:FullVersion-$script:BuildNumber"
        $script:VersionSuffix = "$script:VersionSuffix-$script:BuildNumber"
    }

    Write-Host "`n### Product Version: $script:FullVersion`n" -ForegroundColor Green
}

function BuildForPowerShellVersion($version) {
    # Restore packages for the specified version
    exec { & dotnet restore .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -- /p:PowerShellVersion=$version }

    Write-Host -ForegroundColor Green "`n### Testing API usage for PowerShell $version...`n"
    exec { & dotnet build -f net451 .\src\PowerShellEditorServices\PowerShellEditorServices.csproj -- /p:PowerShellVersion=$version }
}

task TestPowerShellApi {
    BuildForPowerShellVersion v3
    BuildForPowerShellVersion v4
    BuildForPowerShellVersion v5r1

    # Do a final restore to put everything back to normal
    exec { & dotnet restore .\src\PowerShellEditorServices\PowerShellEditorServices.csproj }
}

task BuildHost {
    exec { & dotnet build -c $Configuration .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj }
}

task Build {
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

task PackageNuGet {
    exec { & dotnet pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices\PowerShellEditorServices.csproj }
    exec { & dotnet pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices.Protocol\PowerShellEditorServices.Protocol.csproj }
    exec { & dotnet pack -c $Configuration --version-suffix $script:VersionSuffix .\src\PowerShellEditorServices.Host\PowerShellEditorServices.Host.csproj }
}

task PackageModule {
    Add-Type -Assembly System.IO.Compression.FileSystem -ErrorAction Ignore
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        "$PSScriptRoot/module/PowerShellEditorServices",
        "$PSScriptRoot/module/PowerShellEditorServices-$($script:FullVersion).zip",
        [System.IO.Compression.CompressionLevel]::Optimal,
        $true)
}

task UploadArtifacts -If ($script:IsCIBuild) {
    if ($env:APPVEYOR) {
        Push-AppveyorArtifact .\src\PowerShellEditorServices\bin\$Configuration\Microsoft.PowerShell.EditorServices.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\src\PowerShellEditorServices.Protocol\bin\$Configuration\Microsoft.PowerShell.EditorServices.Protocol.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\src\PowerShellEditorServices.Host\bin\$Configuration\Microsoft.PowerShell.EditorServices.Host.$($script:FullVersion).nupkg
        Push-AppveyorArtifact .\module\PowerShellEditorServices-$($script:FullVersion).zip
    }
}

task UploadTestLogs -If ($script:IsCIBuild) {
    $testLogsZipPath = "$PSScriptRoot/TestLogs.zip"

    Add-Type -Assembly System.IO.Compression.FileSystem -ErrorAction Ignore
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        "$PSScriptRoot/test/PowerShellEditorServices.Test.Host/bin/$Configuration/net451/logs",
        $testLogsZipPath)

    Push-AppveyorArtifact $testLogsZipPath
}

# The default task is to run the entire CI build
task . GetProductVersion, Restore, Clean, Build, TestPowerShellApi, Test, PackageNuGet, PackageModule, UploadArtifacts
