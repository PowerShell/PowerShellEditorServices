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

$Path = "PowerShellEditorServices.Common.props"
$f = Get-Content -Path $Path
$f = $f -replace '^(?<prefix>\s+<VersionPrefix>)(.+)(?<suffix></VersionPrefix>)$', "`${prefix}${v}`${suffix}"
$f = $f -replace '^(?<prefix>\s+<VersionSuffix>)(.*)(?<suffix></VersionSuffix>)$', "`${prefix}$($Version.PreReleaseLabel)`${suffix}"
$f | Set-Content -Path $Path
git add $Path

$Path = "module/PowerShellEditorServices/PowerShellEditorServices.psd1"
$f = Get-Content -Path $Path
$f = $f -replace "^(?<prefix>ModuleVersion = ')(.+)(?<suffix>')`$", "`${prefix}${v}`${suffix}"
$f | Set-Content -Path $Path
git add $Path

$Path = "CHANGELOG.md"
$Changelog = Get-Content -Path $Path
@(
    $Changelog[0..1]
    "## v$Version"
    "### $([datetime]::Now.ToString('dddd, MMMM dd, yyyy'))"
    ""
    "See more details at the GitHub Release for [v$Version](https://github.com/PowerShell/PowerShellEditorServices/releases/tag/v$Version)."
    ""
    $Changes
    ""
    $Changelog[2..$Changelog.Length]
) | Set-Content -Encoding utf8NoBOM -Path $Path
git add $Path

git commit --edit --message "v$($Version): $Changes"
