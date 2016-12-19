param($buildVersion = $null)

$releasePath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\release")
$binariesToSignPath = [System.IO.Path]::GetFullPath("$releasePath\BinariesToSign")
$unpackedPackagesPath = [System.IO.Path]::GetFullPath("$releasePath\UnpackedPackages")

# Ensure that the release path exists and clear out old folders
mkdir $releasePath -Force | Out-Null

# Install prerequisite packages
#Install-Package -Name "Newtonsoft.Json" -RequiredVersion "7.0.1" -Source "nuget.org" -ProviderName "NuGet" -Destination $buildPath -Force

if ($buildVersion -eq $null) {
    # Get the current build status
    $headers = @{ "Content-Type" = "application/json" }
    $project = Invoke-RestMethod -Method Get -Uri "https://ci.appveyor.com/api/projects/PowerShell/PowerShellEditorServices/branch/master" -Headers $headers
    $buildVersion = $project.build.version
    if ($project.build.status -eq "success") {
        Write-Output "Latest build version on master is $buildVersion`r`n"
    }
    else {
        Write-Error "PowerShellEditorServices build $buildVersion was not successful!" -ErrorAction "Stop"
    }
}

function Install-BuildPackage($packageName, $extension) {
	$uri = "https://ci.appveyor.com/nuget/powershelleditorservices/api/v2/package/{0}/{1}" -f $packageName.ToLower(), $buildVersion
	Write-Verbose "Fetching from URI: $uri"

	# Download the package and extract it
	$zipPath = "$releasePath\$packageName.zip"
	$packageContentPath = "$unpackedPackagesPath\$packageName"
	Invoke-WebRequest $uri -OutFile $zipPath -ErrorAction "Stop"
	Expand-Archive $zipPath -DestinationPath $packageContentPath -Force -ErrorAction "Stop"
	Remove-Item $zipPath -ErrorAction "Stop"

	# Copy the binary to the binary signing folder
	mkdir $binariesToSignPath -Force | Out-Null
	cp "$packageContentPath\lib\net45\$packageName.$extension" -Force -Destination $binariesToSignPath

	Write-Output "Extracted package $packageName ($buildVersion)"
}

# Pull the build packages from AppVeyor
Install-BuildPackage "Microsoft.PowerShell.EditorServices" "dll"
Install-BuildPackage "Microsoft.PowerShell.EditorServices.Protocol" "dll"
Install-BuildPackage "Microsoft.PowerShell.EditorServices.Host" "dll"

# Open the BinariesToSign folder
& start $binariesToSignPath
