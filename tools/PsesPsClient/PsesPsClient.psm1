#
# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$script:PsesBundledModulesDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
    "$PSScriptRoot/../../../../module")

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

    return Send-LspRequest -Client $Client -Method 'textDocument/didOpen' -Parameters $parameters
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

    return $Client.WriteRequest($Method, $Parameters)
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
        $WaitMillis = 5000
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
