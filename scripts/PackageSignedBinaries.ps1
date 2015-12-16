# This script assumes the FetchBuildBinaries.ps1 has been run and
# the binaries in 'release\BinariesToSign' have been signed and
# replaced.

param($FailIfNotSigned = $true)

$releasePath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\release")
$finalPackagePath = [System.IO.Path]::GetFullPath("$releasePath\FinalPackages")
$binariesToSignPath = [System.IO.Path]::GetFullPath("$releasePath\BinariesToSign")
$unpackedPackagesPath = [System.IO.Path]::GetFullPath("$releasePath\UnpackedPackages")
$nugetPath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\.nuget\NuGet.exe")

mkdir $finalPackagePath -Force | Out-Null

$binaries = Get-ChildItem $binariesToSignPath
foreach ($binaryPath in $binaries) {
	$signature = Get-AuthenticodeSignature $binaryPath.FullName
	
	# TODO: More validation here?
	if ($signature.Status -eq "NotSigned" -and $FailIfNotSigned) {
		Write-Error "Binary file is not authenticode signed: $binaryPath" -ErrorAction Stop
	}
	
	# Copy the file back where it belongs
	$packageName = [System.IO.Path]::GetFileNameWithoutExtension($binaryPath)
	$packagePath = "$unpackedPackagesPath\$packageName"
	cp $binaryPath.FullName -Destination "$packagePath\lib\net45\" -Force
	
	# Repackage the nupkg with NuGet
	Push-Location $finalPackagePath
	& $nugetPath pack "$packagePath\$packageName.nuspec"
	Pop-Location
}
