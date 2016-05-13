param([switch]$Serve, [switch]$Clean, [switch]$Publish)

$toolsPath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\tools")
$docfxZipPath = [System.IO.Path]::GetFullPath("$toolsPath\docfx.zip")
$docfxBinPath = [System.IO.Path]::GetFullPath("$toolsPath\docfx\")
$docfxExePath = [System.IO.Path]::GetFullPath("$docfxBinPath\docfx.exe")
$docfxJsonPath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\docs\docfx.json")
$sitePath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\docs\_site")
$docsRepoPath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\docs\_repo")

# Ensure the tools path exists
mkdir $toolsPath -Force | Out-Null

if (![System.IO.File]::Exists($docfxExePath)) {
    # Download DocFX
    Remove-Item -Path "$docfxBinPath" -Force -ErrorAction Stop | Out-Null
    (new-object net.webclient).DownloadFile('https://github.com/dotnet/docfx/releases/download/v1.8/docfx.zip', "$docfxZipPath")

    # Extract the archive
    Expand-Archive $docfxZipPath -DestinationPath $docfxBinPath -Force -ErrorAction "Stop"
}

# Clean the _site folder if necessary
if ($Clean.IsPresent) {
    Remove-Item -Path $sitePath -Force -Recurse | Out-Null
}

# Build the metadata for the C# API
& $docfxExePath metadata $docfxJsonPath

if ($Serve.IsPresent) {
    & $docfxExePath build $docfxJsonPath --serve
}
else {
    & $docfxExePath build $docfxJsonPath

    if ($Publish.IsPresent) {
        # Delete the existing docs repo folder
        if (Test-Path $docsRepoPath) {
            Remove-Item -Path $docsRepoPath -Recurse -Force -ErrorAction Stop | Out-Null
        }

        # Clone the documentation site branch of the Editor Services repo
        git clone -b gh-pages https://github.com/PowerShell/PowerShellEditorServices.git $docsRepoPath

        # Copy the site files into the repo path
        Write-Host -ForegroundColor Green "*** Copying documentation site files ***"
        Copy-Item -Recurse -Force $sitePath\* $docsRepoPath

        # Enter the repo path and commit the changes
        Write-Host -ForegroundColor Green "*** Committing changes ***"
        Push-Location $docsRepoPath
        git add -A
        git commit -m "Updated documentation site"

        # Verify that we're ready to push and then do it
        $response =
            $host.ui.PromptForChoice(
                "Ready to push?",
                "Are you ready to push the doc site changes?",
                [System.Management.Automation.Host.ChoiceDescription[]](
                    (New-Object System.Management.Automation.Host.ChoiceDescription "&Yes","Yes"),
                    (New-Object System.Management.Automation.Host.ChoiceDescription "&No","No")
                ),
                1);

        if ($response -eq 0) {
            Write-Host -ForegroundColor Green "*** Pushing changes ***"
            git push origin gh-pages
        }
        else {
            Write-Output "Did not push changes. Run 'git push origin gh-pages' manually when ready."
        }
    }
}
