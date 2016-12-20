param(
    [string[]]$TestProjects = @(
        "PowerShellEditorServices.Test",
        "PowerShellEditorServices.Test.Protocol",
        "PowerShellEditorServices.Test.Host"
    ),

    [ValidateSet("net451", "netstandard1.6")]
    [string]$Framework = "netstandard1.6",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$AppVeyorMode
)

$xunitVersion = "2.2.0-*"

#$nugetPackagesPath = "$HOME/.nuget/packages"
#$testRunnerPath = Join-Path $PSScriptRoot, ".." "test/PowerShellEditorServices.Test.Runner"
#$testRunnerBinPath = Join-Path "$testRunnerPath bin/$Configuration/$Framework"
#$testRunnerDllPath = Join-Path $testRunnerBinPath "Microsoft.PowerShell.EditorServices.Test.Runner.dll"

function Copy-TestDependencies($runnerPath) {

    # Needed depdenendcies:
    # xunit.runner.utility
    # xunit.abstrations
    # xunit.assert

    # TODO: Don't hardcode these paths, resolve them from package.lock.json
    if ($Framework -eq "net451") {
        cp $HOME/.nuget/packages/xunit.runner.utility/2.2.0-beta2-build3300/lib/net45/xunit.runner.utility.desktop.dll $runnerPath
        cp /home/daviwil/.nuget/packages/xunit.abstractions/2.0.1-rc2/lib/net35/xunit.abstractions.dll $runnerPath 
    }
    elseif ($Framework -eq "netstandard1.6") {
        cp $HOME/.nuget/packages/xunit.assert/2.2.0-beta2-build3300/lib/netstandard1.0/xunit.assert.dll $runnerPath
        cp $HOME/.nuget/packages/xunit.runner.utility/2.2.0-beta2-build3300/lib/netstandard1.1/xunit.runner.utility.dotnet.dll $runnerPath                                                            
        cp $HOME/.nuget/packages/xunit.abstractions/2.0.1-rc2/lib/netstandard1.0/xunit.abstractions.dll $runnerPath       
    }

#     $xunitPlatform = "dotnet"

#     $xunitPaths = Get-ChildItem $nugetPackagesPath -Recurse "2.2.0-*" | % { $_.FullName }
#     $xunitDlls = @(
#         "xunit.assert.dll",
#         "xunit.runner.utility.$xunitPlatform"
#     )
#     $xunitDllPaths = Get-ChildItem -Path $xunitPaths -Recurse -Include $xunitDlls
     
#     Copy-Item -Path 
#     #$projectJson = (Get-Content "$testRunnerPath/project.lock.json") -Join "`n" | ConvertFrom-Json
    
}

# Ideals:
# - Provide paths to test assemblies
# - Optionally specify test classes/names to run
# - Maybe use xunit's resolvers if I can, make my own if not
# - Execute tests sequentially at first, maybe in background later
# - Custom output and error handling to get ideal output 
# - Make it possible to use AppVeyor's output system, but if not, use their API:
#    https://www.appveyor.com/docs/build-worker-api/#add-tests

# TODO: Might need to load PSES assemblies too...

$runnerBinPath = Join-Path $PSScriptRoot "../test/PowerShellEditorServices.Test.Runner/bin/$Configuration/$Framework/"
Add-Type -Path "$runnerBinPath/Microsoft.PowerShell.EditorServices.Test.Runner.dll"

Copy-TestDependencies $runnerBinPath

$assemblyPath = Join-Path $PSScriptRoot "../test/PowerShellEditorServices.Test/bin/$Configuration/$Framework/Microsoft.PowerShell.EditorServices.Test.dll"
Get-ChildItem $assemblyPath
Write-Host "Running tests for assembly: $assemblyPath" -ForegroundColor Yellow

$runner = [Microsoft.PowerShell.EditorServices.Test.Runner.TestRunner]::new()
$runner.RunTests(@($assemblyPath))
