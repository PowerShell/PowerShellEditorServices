# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

$copyrightHeaderString =
@'
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
'@

$global:updateCount = 0;

function Add-CopyrightHeaders($basePath)
{
	Push-Location $basePath
	$allSourceFiles = Get-ChildItem $basePath -Recurse -Filter *.cs | ?{ $_.FullName -notmatch "\\obj\\?" }

	foreach ($sourceFile in $allSourceFiles)
	{
		$fileContent = (Get-Content $sourceFile.FullName -Raw).TrimStart()

		if ($fileContent.StartsWith($copyrightHeaderString) -eq $false)
		{
			# Add the copyright header to the file
			Set-Content $sourceFile.FullName ($copyrightHeaderString + [Environment]::NewLine + [Environment]::NewLine + $fileContent)
			Write-Output ("Updated {0}" -f (Resolve-Path $sourceFile.FullName -Relative))
			$global:updateCount++
		}
	}

	Pop-Location
}

Add-CopyrightHeaders(Resolve-Path $PSScriptRoot\..\src)
Add-CopyrightHeaders(Resolve-Path $PSScriptRoot\..\test)

Write-Output "`r`nDone, $global:updateCount file(s) updated."
