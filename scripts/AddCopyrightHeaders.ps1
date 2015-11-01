#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$copyrightHeaderString =
@'
//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
'@

$srcPath = Resolve-Path $PSScriptRoot\..\src
Push-Location $srcPath

$updateCount = 0;
$allSourceFiles = Get-ChildItem $srcPath -Recurse -Filter *.cs | ?{ $_.FullName -notmatch "\\obj\\?" }

foreach ($sourceFile in $allSourceFiles)
{
	$fileContent = (Get-Content $sourceFile.FullName -Raw).TrimStart()

	if ($fileContent.StartsWith($copyrightHeaderString) -eq $false)
	{
		# Add the copyright header to the file
		Set-Content $sourceFile.FullName ($copyrightHeaderString + "`r`n`r`n" + $fileContent)
		Write-Output ("Updated {0}" -f (Resolve-Path $sourceFile.FullName -Relative))
	}
}

Write-Output "`r`nDone, $updateCount files updated."

Pop-Location