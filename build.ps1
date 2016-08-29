param(
    [ValidateSet("net451", "netstandard1.6")]
    [string[]]$Frameworks = $null,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$BuildNumber = "0",

    [switch]$ForceRestore,
    [switch]$PackageNuget,
    [switch]$PackageModule
)

$releasePath = Join-Path $PSScriptRoot "release"
$packagesPath = Join-Path $releasePath "BuiltPackages"
$modulePath = Join-Path $PSScriptRoot "module/PowerShellEditorServices"

if ($Frameworks -eq $null) {
    if ($IsLinux -or $IsOSX) {
        # Running in PowerShell on *NIX
        $Frameworks = @("netstandard1.6")
    }
    else {
        # Running in PowerShell on Windows
        $Frameworks = @("net451", "netstandard1.6")
    }
}

$projectPaths = @(
    "src/PowerShellEditorServices",
    "src/PowerShellEditorServices.Protocol",
    "src/PowerShellEditorServices.Host"
    #"src/PowerShellEditorServices.Channel.WebSocket"
)

$testProjectPaths = @(
    #"test/PowerShellEditorServices.Test",
    #"test/PowerShellEditorServices.Test.Protocol",
    #"test/PowerShellEditorServices.Test.Host"
)

$allProjectPaths = $projectPaths + $testProjectPaths

function Invoke-ProjectBuild($projectPath, $framework) {
    $fullPath = Join-Path $PSScriptRoot $projectPath

    if ($ForceRestore.IsPresent -or !(Test-Path (Join-Path $fullPath "project.lock.json"))) {
        Write-Host "`nRestoring packages for project '$projectPath'" -ForegroundColor Yellow
        Push-Location $fullPath
        & dotnet restore | Out-Null
        Pop-Location

        if ($LASTEXITCODE -ne 0) {
            return $false
        }
    }

    Write-Host "`nBuilding project '$projectPath' for framework '$framework'..." -ForegroundColor Yellow
    $fullPath = Join-Path $PSScriptRoot -ChildPath $projectPath
    Push-Location $fullPath
    & dotnet build --framework $framework --configuration $Configuration --version-suffix $BuildNumber --no-dependencies | Out-Null
    Pop-Location

    return $LASTEXITCODE -eq 0
}

$success = $true
:buildLoop foreach ($projectPath in $allProjectPaths) {
    foreach ($framework in $Frameworks) {
        if (!(Invoke-ProjectBuild $projectPath $framework $Configuration)) {
            Write-Host "`nBuild failed, terminating.`n" -ForegroundColor Red
            $success = $false
            break buildLoop
        }
    }
}

if (!$success) {
    # Error has already been written, make sure to return
    # a non-zero exit code
    exit 1
}

foreach ($framework in $Frameworks) {
    Write-Host "`nCopying '$framework' binaries to module path..." -ForegroundColor Yellow

    $hostBinaryPath = Join-Path $PSScriptRoot "src/PowerShellEditorServices.Host/bin/$Configuration/$framework/*"
    $moduleFrameworkPath = Join-Path $modulePath $framework
    New-Item -ItemType Directory $moduleFrameworkPath -Force | Out-Null
    Copy-Item -Path $hostBinaryPath -Include "*.dll", "*.pdb" -Destination $moduleFrameworkPath -Force
}

# Ensure the packages path exists
if ($PackageModule.IsPresent -or $PackageNuget.IsPresent) {
    New-Item -ItemType Directory $packagesPath -Force | Out-Null
}

if ($PackageModule.IsPresent) {
    Write-Host "`nCreating module package..." -ForegroundColor Yellow

    # TODO: Put version in package name
    $modulePackageName = Join-Path $packagesPath -ChildPath "PowerShellEditorServices-module.zip"
    Compress-Archive -Path $modulePath -DestinationPath $modulePackageName -Force

    # TODO: Handle failure!
}

if ($PackageNuget.IsPresent) {
    Write-Host "`nCreating NuGet packages...`n" -ForegroundColor Yellow

    foreach ($projectPath in $projectPaths) {
        Push-Location (Join-Path $PSScriptRoot $projectPath)
        & dotnet pack -o $packagesPath --no-build --configuration $Configuration --version-suffix $BuildNumber | Out-Null
        Pop-Location

        if ($LASTEXITCODE -ne 0) {
            $success = false
            break
        }
    }
}

if (!$success) {
    # Error has already been written, make sure to return
    # a non-zero exit code
    exit 1
}

Write-Host "`nBuild succeeded!`n" -ForegroundColor Green
