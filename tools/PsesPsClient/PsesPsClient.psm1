#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$script:PsesBundledModulesDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
    "$PSScriptRoot/../../../../module")

Import-Module -Force "$PSScriptRoot/../../../../module/PowerShellEditorServices"
Import-Module -Force (Resolve-Path "$PSScriptRoot/../../../../src/PowerShellEditorServices.Engine/bin/*/netstandard2.0/publish/Omnisharp.Extensions.LanguageProtocol.dll")

class PsesStartupOptions
{
    [string]   $LogPath
    [string]   $LogLevel
    [string]   $SessionDetailsPath
    [string[]] $FeatureFlags
    [string]   $HostName
    [string]   $HostProfileId
    [version]  $HostVersion
    [string[]] $AdditionalModules
    [string]   $BundledModulesPath
    [bool]     $EnableConsoleRepl
    [switch]   $SplitInOutPipes
}

class PsesServerInfo
{
    [pscustomobject]$SessionDetails
    [System.Diagnostics.Process]$PsesProcess
    [PsesStartupOptions]$StartupOptions
    [string]$LogPath
}

function Start-PsesServer
{
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([PsesServerInfo])]
    param(
        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]
        $EditorServicesPath = "$script:PsesBundledModulesDir/PowerShellEditorServices/Start-EditorServices.ps1",

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]
        $LogPath,

        [Parameter()]
        [ValidateSet("Diagnostic", "Normal", "Verbose", "Error")]
        [string]
        $LogLevel = 'Diagnostic',

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]
        $SessionDetailsPath,

        [Parameter()]
        [ValidateNotNull()]
        [string[]]
        $FeatureFlags = @('PSReadLine'),

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]
        $HostName = 'PSES Test Host',

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]
        $HostProfileId = 'TestHost',

        [Parameter()]
        [ValidateNotNull()]
        [version]
        $HostVersion = '1.99',

        [Parameter()]
        [ValidateNotNull()]
        [string[]]
        $AdditionalModules = @('PowerShellEditorServices.VSCode'),

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string]
        $BundledModulesPath,

        [Parameter()]
        [switch]
        $EnableConsoleRepl,

        [Parameter()]
        [string]
        $ErrorFile
    )

    $EditorServicesPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($EditorServicesPath)

    $instanceId = Get-RandomHexString

    $tempDir = [System.IO.Path]::GetTempPath()

    if (-not $LogPath)
    {
        $LogPath = Join-Path $tempDir "pseslogs_$instanceId.log"
    }

    if (-not $SessionDetailsPath)
    {
        $SessionDetailsPath = Join-Path $tempDir "psessession_$instanceId.log"
    }

    if (-not $BundledModulesPath)
    {
        $BundledModulesPath = $script:PsesBundledModulesDir
    }

    $editorServicesOptions = @{
        LogPath = $LogPath
        LogLevel = $LogLevel
        SessionDetailsPath = $SessionDetailsPath
        FeatureFlags = $FeatureFlags
        HostName = $HostName
        HostProfileId = $HostProfileId
        HostVersion = $HostVersion
        AdditionalModules = $AdditionalModules
        BundledModulesPath = $BundledModulesPath
        EnableConsoleRepl = $EnableConsoleRepl
        SplitInOutPipes = [switch]::Present
    }

    $startPsesCommand = Unsplat -Prefix "& '$EditorServicesPath'" -SplatParams $editorServicesOptions

    $pwshPath = (Get-Process -Id $PID).Path

    if (-not $PSCmdlet.ShouldProcess("& '$pwshPath' -Command '$startPsesCommand'"))
    {
        return
    }

    $startArgs = @(
        '-NoLogo',
        '-NoProfile',
        '-NoExit',
        '-Command',
        $startPsesCommand
    )

    $startProcParams = @{
        PassThru = $true
        FilePath = $pwshPath
        ArgumentList = $startArgs
    }

    if ($ErrorFile)
    {
        $startProcParams.RedirectStandardError = $ErrorFile
    }

    $serverProcess = Start-Process @startProcParams

    $sessionPath = $editorServicesOptions.SessionDetailsPath

    $i = 0
    while (-not (Test-Path $sessionPath))
    {
        if ($i -ge 10)
        {
            throw "No session file found - server failed to start"
        }

        Start-Sleep 1
        $null = $i++
    }

    return [PsesServerInfo]@{
        PsesProcess = $serverProcess
        SessionDetails = Get-Content -Raw $editorServicesOptions.SessionDetailsPath | ConvertFrom-Json
        StartupOptions = $editorServicesOptions
        LogPath = $LogPath
    }
}

function Connect-PsesServer
{
    [OutputType([PsesPsClient.PsesLspClient])]
    param(
        [Parameter(Mandatory)]
        [string]
        $InPipeName,

        [Parameter(Mandatory)]
        [string]
        $OutPipeName
    )

    $psesIdx = $InPipeName.IndexOf('PSES')
    if ($psesIdx -gt 0)
    {
        $InPipeName = $InPipeName.Substring($psesIdx)
        $OutPipeName = $OutPipeName.Substring($psesIdx)
    }

    $client = [PsesPsClient.PsesLspClient]::Create($InPipeName, $OutPipeName)
    $client.Connect()
    return $client
}

function Send-LspInitializeRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter()]
        [int]
        $ProcessId = $PID,

        [Parameter()]
        [string]
        $RootPath = (Get-Location),

        [Parameter()]
        [string]
        $RootUri,

        [Parameter()]
        [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.ClientCapabilities]
        $ClientCapabilities = (Get-ClientCapabilities),

        [Parameter()]
        [object]
        $IntializationOptions = ([object]::new())
    )

    $parameters = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.InitializeParams]@{
        ProcessId = $ProcessId
        Capabilities = $ClientCapabilities
        InitializationOptions = $IntializationOptions
    }

    if ($RootUri)
    {
        $parameters.RootUri = $RootUri
    }
    else
    {
        $parameters.RootUri = [uri]::new($RootPath)
    }

    return Send-LspRequest -Client $Client -Method 'initialize' -Parameters $parameters
}

function Send-LspDidOpenTextDocumentRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Mandatory)]
        [string]
        $Uri,

        [Parameter()]
        [int]
        $Version = 0,

        [Parameter()]
        [string]
        $LanguageId = "powershell",

        [Parameter()]
        [string]
        $Text
    )

    $parameters = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DidOpenTextDocumentParams]@{
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentItem]@{
            Uri = $Uri
            LanguageId = $LanguageId
            Text = $Text
            Version = $Version
        }
    }

    $result = Send-LspRequest -Client $Client -Method 'textDocument/didOpen' -Parameters $parameters

    # Give PSScriptAnalyzer enough time to run
    Start-Sleep -Seconds 1

    $result
}

function Send-LspDidChangeConfigurationRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Mandatory)]
        [Microsoft.PowerShell.EditorServices.Protocol.Server.LanguageServerSettingsWrapper]
        $Settings
    )

    $parameters = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DidChangeConfigurationParams[Microsoft.PowerShell.EditorServices.Protocol.Server.LanguageServerSettingsWrapper]]@{
        Settings = $Settings
    }

    $result = Send-LspRequest -Client $Client -Method 'workspace/didChangeConfiguration' -Parameters $parameters

    # Give PSScriptAnalyzer enough time to run
    Start-Sleep -Seconds 1

    $result
}

function Send-LspFormattingRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Mandatory)]
        [string]
        $Uri,

        [Parameter()]
        [int]
        $TabSize = 4,

        [Parameter()]
        [switch]
        $InsertSpaces
    )

    $params = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DocumentFormattingParams]@{
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier]@{
            Uri = $Uri
        }
        options = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.FormattingOptions]@{
            TabSize = $TabSize
            InsertSpaces = $InsertSpaces.IsPresent
        }
    }

    return Send-LspRequest -Client $Client -Method 'textDocument/formatting' -Parameters $params
}

function Send-LspRangeFormattingRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Mandatory)]
        [string]
        $Uri,

        [Parameter(Mandatory)]
        [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Range]
        $Range,

        [Parameter()]
        [int]
        $TabSize = 4,

        [Parameter()]
        [switch]
        $InsertSpaces
    )

    $params = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DocumentRangeFormattingParams]@{
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier]@{
            Uri = $Uri
        }
        Range = $Range
        options = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.FormattingOptions]@{
            TabSize = $TabSize
            InsertSpaces = $InsertSpaces.IsPresent
        }
    }

    return Send-LspRequest -Client $Client -Method 'textDocument/rangeFormatting' -Parameters $params
}

function Send-LspDocumentSymbolRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Mandatory)]
        [string]
        $Uri
    )

    $params = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DocumentSymbolParams]@{
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier]@{
            Uri = $Uri
        }
    }
    return Send-LspRequest -Client $Client -Method 'textDocument/documentSymbol' -Parameters $params
}

function Send-LspReferencesRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Mandatory)]
        [string]
        $Uri,

        [Parameter(Mandatory)]
        [int]
        $LineNumber,

        [Parameter(Mandatory)]
        [int]
        $CharacterNumber,

        [Parameter()]
        [switch]
        $IncludeDeclaration
    )

    $params = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.ReferencesParams]@{
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier]@{
            Uri = $Uri
        }
        Position = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
            Line = $LineNumber
            Character = $CharacterNumber
        }
        Context = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.ReferencesContext]@{
            IncludeDeclaration = $IncludeDeclaration
        }
    }
    return Send-LspRequest -Client $Client -Method 'textDocument/references' -Parameters $params
}

function Send-LspDocumentHighlightRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Position = 1, Mandatory)]
        [string]
        $Uri,

        [Parameter(Mandatory)]
        [int]
        $LineNumber,

        [Parameter(Mandatory)]
        [int]
        $CharacterNumber
    )

    $documentHighlightParams = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentPositionParams]@{
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier]@{
            Uri = $Uri
        }
        Position = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
            Line = $LineNumber
            Character = $CharacterNumber
        }
    }

    return Send-LspRequest -Client $Client -Method 'textDocument/documentHighlight' -Parameters $documentHighlightParams
}

function Send-LspGetRunspaceRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Mandatory)]
        [int]
        $ProcessId
    )

    $params = [PowerShellEditorServices.Engine.Services.Handlers.GetRunspaceParams]@{
        ProcessId = $ProcessId
    }
    return Send-LspRequest -Client $Client -Method 'powerShell/getRunspace' -Parameters $params
}

function Send-LspCodeLensRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [string]
        $Uri
    )

    $params = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.CodeLensRequest]@{
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier]@{
            Uri = $Uri
        }
    }
    return Send-LspRequest -Client $Client -Method 'textDocument/codeLens' -Parameters $params
}

function Send-LspCodeLensResolveRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Mandatory)]
        # Expects to be passed in a single item from the `Result` collection from
        # Send-LspCodeLensRequest
        $CodeLens
    )

    $params = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.CodeLens]@{
        Data = [Newtonsoft.Json.Linq.JToken]::Parse(($CodeLens.data.data | ConvertTo-Json))
        Range = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Range]@{
            Start = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
                Line = $CodeLens.range.start.line
                Character = $CodeLens.range.start.character
            }
            End = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
                Line = $CodeLens.range.end.line
                Character = $CodeLens.range.end.character
            }
        }
    }

    return Send-LspRequest -Client $Client -Method 'codeLens/resolve' -Parameters $params
}

function Send-LspCodeActionRequest
{
    param(
        [Parameter()]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter()]
        [string]
        $Uri,

        [Parameter()]
        [int]
        $StartLine,

        [Parameter()]
        [int]
        $StartCharacter,

        [Parameter()]
        [int]
        $EndLine,

        [Parameter()]
        [int]
        $EndCharacter,

        [Parameter()]
        $Diagnostics
    )

    $params = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.CodeActionParams]@{
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier]@{
            Uri = $Uri
        }
        Range = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Range]@{
            Start = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
                Line = $StartLine
                Character = $StartCharacter
            }
            End = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
                Line = $EndLine
                Character = $EndCharacter
            }
        }
        Context = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.CodeActionContext]@{
            Diagnostics = $Diagnostics | ForEach-Object {
                [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Diagnostic]@{
                    Code = $_.code
                    Severity = $_.severity
                    Source = $_.source
                    Message = $_.message
                    Range = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Range]@{
                        Start = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
                            Line = $_.range.start.line
                            Character = $_.range.start.character
                        }
                        End = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
                            Line = $_.range.end.line
                            Character = $_.range.end.character
                        }
                    }
                }
            }
        }
    }

    return Send-LspRequest -Client $Client -Method 'textDocument/codeAction' -Parameters $params
}

function Send-LspShutdownRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client
    )

    return Send-LspRequest -Client $Client -Method 'shutdown'
}

function Send-LspRequest
{
    [OutputType([PsesPsClient.LspRequest])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Position = 1, Mandatory)]
        [string]
        $Method,

        [Parameter(Position = 2)]
        $Parameters = $null
    )


    $result = $Client.WriteRequest($Method, $Parameters)

    # To allow for result/notification queue to fill up
    Start-Sleep 1

    $result
}

function Get-LspResponse
{
    [OutputType([PsesPsClient.LspResponse])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client,

        [Parameter(Position = 1, Mandatory)]
        [string]
        $Id,

        [Parameter()]
        [int]
        $WaitMillis = 10000
    )

    $lspResponse = $null

    if ($Client.TryGetResponse($Id, [ref]$lspResponse, $WaitMillis))
    {
        $result = if ($lspResponse.Result) { $lspResponse.Result.ToString() | ConvertFrom-Json }
        return [PSCustomObject]@{
            Id = $lspResponse.Id
            Result = $result
        }
    }
}

function Get-LspNotification
{
    [OutputType([PsesPsClient.LspResponse])]
    param(
        [Parameter(Position = 0, Mandatory)]
        [PsesPsClient.PsesLspClient]
        $Client
    )

    $Client.GetNotifications() | ForEach-Object {
        $result = if ($_.Params) { $_.Params.ToString() | ConvertFrom-Json }
        [PSCustomObject]@{
            Method = $_.Method
            Params = $result
        }
    }
}

function Unsplat
{
    param(
        [string]$Prefix,
        [hashtable]$SplatParams)

    $sb = New-Object 'System.Text.StringBuilder' ($Prefix)

    foreach ($key in $SplatParams.get_Keys())
    {
        $val = $SplatParams[$key]

        if (-not $val)
        {
            continue
        }

        $null = $sb.Append(" -$key")

        if ($val -is [switch])
        {
            continue
        }

        if ($val -is [array])
        {
            $null = $sb.Append(' @(')
            for ($i = 0; $i -lt $val.Count; $i++)
            {
                $null = $sb.Append("'").Append($val[$i]).Append("'")
                if ($i -lt $val.Count - 1)
                {
                    $null = $sb.Append(',')
                }
            }
            $null = $sb.Append(')')
            continue
        }

        if ($val -is [version])
        {
            $val = [string]$val
        }

        if ($val -is [string])
        {
            $null = $sb.Append(" '$val'")
            continue
        }

        throw "Bad value '$val' of type $($val.GetType())"
    }

    return $sb.ToString()
}

$script:Random = [System.Random]::new()
function Get-RandomHexString
{
    param([int]$Length = 10)

    $buffer = [byte[]]::new($Length / 2)
    $script:Random.NextBytes($buffer)
    $str = ($buffer | ForEach-Object { "{0:x02}" -f $_ }) -join ''

    if ($Length % 2 -ne 0)
    {
        $str += ($script:Random.Next() | ForEach-Object { "{0:02}" -f $_ })
    }

    return $str
}

function Get-ClientCapabilities
{
    return [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.ClientCapabilities]@{
        Workspace = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.WorkspaceClientCapabilities]@{
            ApplyEdit = $true
            WorkspaceEdit = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.WorkspaceEditCapabilities]@{
                DocumentChanges = $false
            }
            DidChangeConfiguration = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            DidChangeWatchedFiles = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            Symbol = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            ExecuteCommand = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
        }
        TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentClientCapabilities]@{
            Synchronization = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.SynchronizationCapabilities]@{
                WillSave = $true
                WillSaveWaitUntil = $true
                DidSave = $true
            }
            Completion = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.CompletionCapabilities]@{
                DynamicRegistration = $false
                CompletionItem = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.CompletionItemCapabilities]@{
                    SnippetSupport = $true
                }
            }
            Hover = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            SignatureHelp = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            References = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            DocumentHighlight = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            DocumentSymbol = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            Formatting = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            RangeFormatting = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            OnTypeFormatting = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            Definition = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            CodeLens = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            CodeAction = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            DocumentLink = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
            Rename = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.DynamicRegistrationCapability]@{
                DynamicRegistration = $false
            }
        }
        Experimental = [System.Object]::new()
    }
}
