class NamedPipeWrapper
{
    NamedPipeWrapper(
        [System.IO.Pipes.NamedPipeClientStream]
        $namedPipeClient
    )
    {
        $this.NamedPipeClient = $namedPipeClient
    }

    hidden [System.IO.Pipes.NamedPipeClientStream] $NamedPipeClient

    hidden [System.IO.StreamReader] $Reader

    hidden [System.IO.StreamWriter] $Writer

    hidden [char[]] $ReaderBuffer

    Connect()
    {
        $this.NamedPipeClient.Connect(1000)

        $encoding = [System.Text.UTF8Encoding]::new($false)
        $this.Reader = [System.IO.StreamReader]::new($this.NamedPipeClient, $encoding)
        $this.Writer = [System.IO.StreamWriter]::new($this.NamedPipeClient, $encoding)
        $this.Writer.AutoFlush = $true
        $this.ReaderBuffer = [char[]]::new(1024)
    }

    Write([string]$message)
    {
        $this.Writer.Write($message)
    }

    [bool]
    HasContent()
    {
        return $this.Reader.Peek() -gt 0
    }

    [string]
    ReadMessage()
    {
        $sb = [System.Text.StringBuilder]::new()

        $charCount = $this.Reader.Peek()
        $remainingMessageChars = -1
        while ($remainingMessageChars -ne 0 -and $charCount -gt 0)
        {
            if ($charCount -gt $this.ReaderBuffer.Length)
            {
                [array]::Resize([ref]$this.ReaderBuffer, $this.ReaderBuffer.Length * 2)
            }

            $this.Reader.Read($this.ReaderBuffer, 0, $charCount)

            $sb.Append($this.ReaderBuffer, 0, $charCount)

            if ($remainingMessageChars -ge 0)
            {
                $remainingMessageChars -= $charCount
            }
            else
            {
                $msgSoFar = $sb.ToString()
                $endHeaderIdx = $msgSoFar.IndexOf("`r`n`r`n")

                if ($endHeaderIdx -ge 0)
                {
                    $remainingMessageChars = [int]($msgSoFar.Substring(16, $endHeaderIdx - 16))

                    $overflowLength = $charCount - ($endHeaderIdx + 4)

                    if ($overflowLength -gt 0)
                    {
                        $remainingMessageChars -= $overflowLength
                    }
                }
            }

            $charCount = [System.Math]::Min($remainingMessageChars, $this.Reader.Peek())
        }

        return $sb.ToString()
    }

    Dispose()
    {
        $this.Reader.Dispose()
        $this.Writer.Dispose()
        $this.NamedPipeClient.Dispose()
    }
}

$script:PsesBundledModulesDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
    "$PSScriptRoot/../../../PowerShellEditorServices")

Import-Module "$script:PsesBundledModulesDir/PowerShellEditorServices"

function Start-PsesServer
{
    [CmdletBinding(SupportsShouldProcess)]
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
        $EnableConsoleRepl
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
    }

    $startPsesCommand = Unsplat -Prefix "& '$EditorServicesPath'" -SplatParams $editorServicesOptions

    $pwshPath = (Get-Process -Id $PID).Path

    if (-not $PSCmdlet.ShouldProcess("& '$pwshPath' -Command '$startPsesCommand'"))
    {
        return
    }

    $serverProcess = Start-Process -PassThru -FilePath $pwshPath -ArgumentList @(
        '-NoLogo',
        '-NoProfile',
        '-NoExit',
        '-Command',
        $startPsesCommand
    )

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

    return @{
        Process = $serverProcess
        SessionDetails = Get-Content -Raw $editorServicesOptions.SessionDetailsPath | ConvertFrom-Json
        StartupOptions = $editorServicesOptions
    }
}

function Connect-NamedPipe
{
    param(
        [Parameter(Mandatory)]
        [string]
        $PipeName
    )

    $psesIdx = $PipeName.IndexOf('PSES')
    if ($psesIdx -gt 0)
    {
        $PipeName = $PipeName.Substring($psesIdx)
    }

    $pipe = [NamedPipeWrapper]::new(([System.IO.Pipes.NamedPipeClientStream]::new('.', $PipeName, 'InOut')))
    $pipe.Connect()
    return $pipe
}

function Send-LspInitializeRequest
{
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter()]
        [NamedPipeWrapper]
        $Pipe,

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
        $ClientCapabilities = ([Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.ClientCapabilities]::new()),

        [Parameter()]
        [hashtable]
        $InitializeOptions = $null
    )

    $parameters = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.InitializeParams]@{
        ProcessId = $ProcessId
        Capabilities = $ClientCapabilities
        InitializeOptions = $InitializeOptions
    }

    if ($RootUri)
    {
        $parameters.RootUri = $RootUri
    }
    else
    {
        $parameters.RootPath = $RootPath
    }

    Send-LspRequest -Pipe $Pipe -Method 'initialize' -Parameters $parameters -WhatIf:$PSBoundParameters.ContainsKey('WhatIf')
}

$script:MessageId = -1
$script:JsonSerializerSettings = [Newtonsoft.Json.JsonSerializerSettings]@{
    ContractResolver = [Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver]::new()
}
$script:Utf8Encoding = [System.Text.UTF8Encoding]::new($false)
$script:JsonSerializer = [Newtonsoft.Json.JsonSerializer]::Create($script:JsonSerializerSettings)
$script:JsonRpcSerializer = [Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Serializers.JsonRpcMessageSerializer]::new()
function Send-LspRequest
{
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter()]
        [NamedPipeWrapper]
        $Pipe,

        [Parameter()]
        [string]
        $Method,

        [Parameter()]
        [object]
        $Parameters
    )

    $null = $script:MessageId++

    $msg = [Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Message]::Request(
        $script:MessageId,
        $Method,
        [Newtonsoft.Json.Linq.JToken]::FromObject($Parameters, $JsonSerializer))

    $msgJson = $script:JsonRpcSerializer.SerializeMessage($msg)
    $msgString = [Newtonsoft.Json.JsonConvert]::SerializeObject($msgJson, $script:JsonSerializerSettings)
    $msgBytes = $script:Utf8Encoding.GetBytes($msgString)

    $header = "Content-Length: $($msgBytes.Length)`r`n`r`n"
    $headerBytes = $script:Utf8Encoding.GetBytes($header)

    $bytesToSend = $headerBytes + $msgBytes

    if (-not $PSCmdlet.ShouldProcess("Send '$Method' message to server"))
    {
        return $script:Utf8Encoding.GetString($bytesToSend)
    }

    $Pipe.Write($bytesToSend)
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
    $str = ($buffer | % { "{0:x02}" -f $_ }) -join ''

    if ($Length % 2 -ne 0)
    {
        $str += ($script:Random.Next() | % { "{0:02}" -f $_ })
    }

    return $str
}
