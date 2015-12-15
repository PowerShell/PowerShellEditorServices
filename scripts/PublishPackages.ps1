
$releasePath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\release")
$finalPackagePath = [System.IO.Path]::GetFullPath("$releasePath\FinalPackages")
$nugetPath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\.nuget\NuGet.exe")

$packages = Get-ChildItem $finalPackagePath -Filter "*.nupkg"

foreach ($package in $packages) {
    & $nugetPath push -NonInteractive $package.FullName
    if ($LASTEXITCODE -ne 0)
    {
        Write-Output "`r`n'nuget push' has failed.  You may need to run the following command to set the NuGet account API key:`r`n"
        Write-Output "& $nuGetPath setApiKey"
        break
    }
    else
    {
        Write-Output "Pushed package $package.FullName"   
    }
}

# TODO: Use Find-Package to verify that the package is there?