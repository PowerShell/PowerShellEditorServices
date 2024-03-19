# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [Parameter(Mandatory)]
    [semver]$Version,

    [Parameter(Mandatory)]
    [string]$Changes
)

git diff --staged --quiet --exit-code
if ($LASTEXITCODE -ne 0) {
    throw "There are staged changes in the repository. Please commit or reset them before running this script."
}

$v = "$($Version.Major).$($Version.Minor).$($Version.Patch)"

$path = "PowerShellEditorServices.Common.props"
$f = Get-Content -Path $path
$f = $f -replace '^(?<prefix>\s+<VersionPrefix>)(.+)(?<suffix></VersionPrefix>)$', "`${prefix}${v}`${suffix}"
$f = $f -replace '^(?<prefix>\s+<VersionSuffix>)(.*)(?<suffix></VersionSuffix>)$', "`${prefix}$($Version.PreReleaseLabel)`${suffix}"
$f | Set-Content -Path $path
git add $path

$path = "module/PowerShellEditorServices/PowerShellEditorServices.psd1"
$f = Get-Content -Path $path
$f = $f -replace "^(?<prefix>ModuleVersion = ')(.+)(?<suffix>')`$", "`${prefix}${v}`${suffix}"
$f | Set-Content -Path $path
git add $path

$path = "CHANGELOG.md"
$Changelog = Get-Content -Path $path
@(
    $Changelog[0..1]
    "## v$Version"
    "### $([datetime]::Now.ToString('dddd, MMMM dd, yyyy'))"
    ""
    $Changes
    ""
    $Changelog[2..$Changelog.Length]
) | Set-Content -Encoding utf8NoBOM -Path $path
git add $path

git commit --edit --message "v$($Version): $Changes"
