param(
    [Parameter(Mandatory)]
    [version]
    $ExtensionVersion,

    [Parameter()]
    [string]
    $GalleryFileName = 'extensionsGallery.json',

    [Parameter(Mandatory)]
    [string]
    $GitHubToken
)

$ErrorActionPreference = 'Stop'

function CloneADSRepo
{
    param(
        [Parameter(Mandatory)]
        [string]
        $Destination,

        [Parameter()]
        [string]
        $Branch = 'release/extensions',

        [Parameter()]
        [string]
        $Origin = 'https://github.com/rjmholt/AzureDataStudio',

        [Parameter()]
        [string]
        $Upstream = 'https://github.com/Microsoft/AzureDataStudio',

        [Parameter(Mandatory)]
        [string]
        $CheckoutBranch,

        [switch]
        $Clobber
    )

    $Destination = $PSCmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)

    if (Test-Path $Destination)
    {
        if (-not $Clobber)
        {
            throw "File already exists at path '$Destination'"
        }

        Remove-Item -Force -Recurse $Destination
    }

    git clone --single-branch --branch $Branch $Origin $Destination
    Push-Location $Destination
    try
    {
        git remote add upstream $Upstream
        git pull $Upstream $Branch
        git push $Origin $Branch
        git checkout -b $CheckoutBranch
    }
    finally
    {
        Pop-Location
    }
}

function NewReleaseVersionEntry
{
    param(
        [Parameter()]
        [version]
        $Version,

        [Parameter()]
        [datetime]
        $UpdateDate = [datetime]::Now.Date
    )

    return @{
        version = "$Version"
        lastUpdated = $UpdateDate.ToString('M/dd/yyyy')
        assetUri = ''
        fallbackAssetUri = 'fallbackAssetUri'
        files = @(
            @{
                assetType = 'Microsoft.VisualStudio.Services.VSIXPackage'
                source = "https://sqlopsextensions.blob.core.windows.net/extensions/powershell/PowerShell-$Version.vsix"
            }
            @{
                assetType = 'Microsoft.VisualStudio.Services.Icons.Default'
                source = 'https://raw.githubusercontent.com/PowerShell/vscode-powershell/master/images/PowerShell_icon.png'
            }
            @{
                assetType = 'Microsoft.VisualStudio.Services.Content.Details'
                source = 'https://raw.githubusercontent.com/PowerShell/vscode-powershell/master/docs/azure_data_studio/README_FOR_MARKETPLACE.md'
            }
            @{
                assetType = 'Microsoft.VisualStudio.Code.Manifest'
                source = 'https://raw.githubusercontent.com/PowerShell/vscode-powershell/master/package.json'
            }
            @{
                assetType = 'Microsoft.VisualStudio.Services.Content.License'
                source = 'https://raw.githubusercontent.com/PowerShell/vscode-powershell/master/LICENSE.txt'
            }
        )
        properties = @(
            @{
                key = 'Microsoft.VisualStudio.Code.ExtensionDependencies'
                value = ''
            }
            @{
                key = 'Microsoft.VisualStudio.Code.Engine'
                value = '>=0.32.1'
            }
            @{
                key = 'Microsoft.VisualStudio.Services.Links.Source'
                value = 'https://github.com/PowerShell/vscode-powershell/'
            }
        )
    }
}

function NewPowerShellExtensionEntry
{
    param(
        [Parameter()]
        [version]
        $ExtensionVersion
    )

    return @{
        extensionId = 35
        extensionName = 'PowerShell'
        displayName = 'PowerShell'
        shortDescription = 'Develop PowerShell scripts in Azure Data Studio'
        publisher = @{
            displayName = 'Microsoft'
            publisherId = 'Microsoft'
            publisherName = 'Microsoft'
        }
        versions = @(
            NewReleaseVersionEntry -Version $ExtensionVersion
        )
        statistics = @()
        flags = 'preview'
    }

}

function FindPSExtensionJsonSpan
{
    param(
        [Parameter()]
        [string]
        $GalleryExtensionFileContent
    )

    try
    {
        $reader = [System.IO.StringReader]::new($GalleryExtensionFileContent)
        $jsonReader = [Newtonsoft.Json.JsonTextReader]::new($reader)

        $depth = 0
        $startLine = -1
        $startColumn = -1
        $startDepth = -1
        $awaitingExtensionName = $false
        $foundPowerShell = $false
        while ($jsonReader.Read())
        {
            switch ($jsonReader.TokenType)
            {
                'StartObject'
                {
                    if (-not $foundPowerShell)
                    {
                        $startDepth = $depth
                        $startLine = $jsonReader.LineNumber
                        $startColumn = $jsonReader.LinePosition
                    }
                    $depth++
                    continue
                }

                'EndObject'
                {
                    if ($foundPowerShell -and $depth -eq $startDepth + 1)
                    {
                        return @{
                            Start = @{
                                Line = $startLine
                                Column = $startColumn
                            }
                            End = @{
                                Line = $jsonReader.LineNumber
                                Column = $jsonReader.LinePosition
                            }
                        }
                    }
                    $depth--
                    continue
                }

                'PropertyName'
                {
                    if ($jsonReader.Value -eq 'extensionName')
                    {
                        $awaitingExtensionName = $true
                    }
                    continue
                }

                'String'
                {
                    if (-not $awaitingExtensionName)
                    {
                        continue
                    }

                    $awaitingExtensionName = $false

                    if ($jsonReader.Value -eq 'PowerShell')
                    {
                        $foundPowerShell = $true
                    }

                    continue
                }
            }
        }
    }
    finally
    {
        $reader.Dispose()
        $jsonReader.Dispose()
    }

    throw 'Did not find PowerShell extension'
}

function GetStringOffsetFromSpan
{
    param(
        [Parameter()]
        [string]
        $String,

        [Parameter()]
        [int]
        $EndLine,

        [Parameter()]
        [int]
        $StartLine = 1,

        [Parameter()]
        [int]
        $Column = 0,

        [Parameter()]
        [int]
        $InitialOffset = 0
    )

    $lfChar = 0xA

    $idx = $InitialOffset
    $spanLines = $EndLine - $StartLine
    for ($i = 0; $i -lt $spanLines; $i++)
    {
        $idx = $String.IndexOf($lfChar, $idx + 1)

        if ($idx -lt 0)
        {
            return $idx
        }
    }

    return $idx + $Column
}

function ReplaceStringSegment
{
    param(
        [Parameter(Mandatory)]
        [string]
        $String,

        [Parameter(Mandatory)]
        [string]
        $NewSegment,

        [Parameter(Mandatory)]
        [int]
        $StartIndex,

        [Parameter(Mandatory)]
        [int]
        $EndIndex
    )

    $indentBuilder = [System.Text.StringBuilder]::new()
    $indentIdx = $StartIndex - 1
    $currChar = $String[$indentIdx]
    while ($currChar -ne "`n")
    {
        $null = $indentBuilder.Append($currChar)
        $indentIdx--
        $currChar = $String[$indentIdx]
    }
    $indent = $indentBuilder.ToString()

    $newStringBuilder = [System.Text.StringBuilder]::new()
    $null = $newStringBuilder.Append($String.Substring(0, $StartIndex))

    $segmentLines = $NewSegment.Split("`n")
    $null = $newStringBuilder.Append($segmentLines[0]).Append("`n")
    $i = 1
    for (; $i -lt $segmentLines.Length - 1; $i++)
    {
        $null = $newStringBuilder.Append($indent).Append($segmentLines[$i]).Append("`n")
    }
    $null = $newStringBuilder.Append($indent).Append($segmentLines[$i])

    $null = $newStringBuilder.Append($String.Substring($EndIndex+1))

    return $newStringBuilder.ToString()
}

function UpdateGalleryFile
{
    param(
        [Parameter(Mandatory)]
        [version]
        $ExtensionVersion,

        [Parameter()]
        [string]
        $GalleryFilePath = './extensionsGallery-insider.json'
    )

    # Create a new PowerShell extension entry
    $powershellEntry = NewPowerShellExtensionEntry -ExtensionVersion $ExtensionVersion
    $powershellEntryJson = ConvertTo-Json $powershellEntry -Depth 100

    # Reformat the entry with tab-based indentation, like the existing file
    $jObject = [Newtonsoft.Json.Linq.JObject]::Parse([string]$powershellEntryJson)
    $stringBuilder = [System.Text.StringBuilder]::new()
    try
    {
        $stringWriter = [System.IO.StringWriter]::new($stringBuilder)
        $jsonWriter = [Newtonsoft.Json.JsonTextWriter]::new($stringWriter)
        $jsonWriter.Indentation = 1
        $jsonWriter.IndentChar = "`t"
        $jsonWriter.Formatting = 'Indented'
        $null = $jObject.WriteTo($jsonWriter)
    }
    finally
    {
        $jsonWriter.Dispose()
        $stringWriter.Dispose()
    }
    $entryStr = $stringBuilder.ToString()

    # Find the position in the existing file where the PowerShell extension should go
    $galleryFileContent = Get-Content -Raw $GalleryFilePath
    $span = FindPSExtensionJsonSpan -GalleryExtensionFileContent $galleryFileContent
    $startOffset = GetStringOffsetFromSpan -String $galleryFileContent -EndLine $span.Start.Line -Column $span.Start.Column
    $endOffset = GetStringOffsetFromSpan -String $galleryFileContent -EndLine $span.End.Line -StartLine $span.Start.Line -Column $span.End.Column -InitialOffset $startOffset

    # Create the new file contents with the inserted segment
    $newGalleryFileContent = ReplaceStringSegment -String $galleryFileContent -NewSegment $entryStr -StartIndex $startOffset -EndIndex $endOffset

    # Write out the new entry
    [System.IO.File]::WriteAllText($GalleryFilePath, $newGalleryFileContent, [System.Text.UTF8Encoding]::new(<# BOM #> $false))
}

function CommitAndPushChanges
{
    param(
        [Parameter()]
        [string[]]
        $File,

        [Parameter()]
        [string]
        $Message,

        [Parameter()]
        [string]
        $Branch
    )

    Push-Location $repoLocation
    try
    {
        git add $File
        git commit -m $Message
        git push origin $Branch
    }
    finally
    {
        Pop-Location
    }
}

function OpenGitHubPr
{
    param(
        [Parameter(Mandatory)]
        [string]
        $Branch,

        [Parameter(Mandatory)]
        [string]
        $Title,

        [Parameter(Mandatory)]
        [string]
        $Description,

        [Parameter(Mandatory)]
        [string]
        $GitHubToken,

        [Parameter()]
        [string]
        $FromOrg = 'rjmholt',

        [Parameter()]
        [string]
        $TargetBranch = 'release/extensions',

        [Parameter()]
        [string]
        $Organization = 'Microsoft',

        [Parameter()]
        $Repository = 'AzureDataStudio'
    )

    $uri = "https://api.github.com/repos/$Organization/$Repository/pulls"

    if ($FromOrg)
    {
        $Branch = "${FromOrg}:${TargetBranch}"
    }

    $body = @{
        title = $Title
        body = $Description
        head = $Branch
        base = $TargetBranch
    } | ConvertTo-Json

    $headers = @{
        Accept = 'application/vnd.github.v3+json'
        Authorization = "token $GitHubToken"
    }

    Invoke-RestMethod -Method Post -Uri $uri -Body $body -Headers $headers
}

$repoLocation = Join-Path ([System.IO.Path]::GetTempPath()) 'ads-temp-checkout'
$branchName = "update-psext-$ExtensionVersion"

CloneADSRepo -Destination $repoLocation -CheckoutBranch $branchName -Clobber

UpdateGalleryFile -ExtensionVersion $ExtensionVersion -GalleryFilePath "$repoLocation/$GalleryFileName"

CommitAndPushChanges -File $GalleryFileName -Branch $branchName -Message "Update PS extension to v$ExtensionVersion"

$prParams = @{
    Branch = $branchName
    Title = "Update PowerShell extension to v$ExtensionVersion"
    Description = "Updates the version of the PowerShell extension in ADS to $ExtensionVersion.`n**Note**: This is an automated PR."
    Organization = 'rjmholt'
    GitHubToken = $GitHubToken
}
OpenGitHubPr @prParams
