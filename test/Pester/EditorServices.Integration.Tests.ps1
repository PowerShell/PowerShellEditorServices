
$script:ExceptionRegex = [regex]::new('\s*Exception: (.*)$', 'Compiled,Multiline,IgnoreCase')
function ReportLogErrors
{
    param(
        [Parameter()][string]$LogPath,

        [Parameter()][ref]<#[int]#>$FromIndex = 0,

        [Parameter()][string[]]$IgnoreException = @()
    )

    $logEntries = Parse-PsesLog $LogPath |
        Where-Object Index -ge $FromIndex.Value

    # Update the index to the latest in the log
    $FromIndex.Value = ($FromIndex.Value,$errorLogs.Index | Measure-Object -Maximum).Maximum

    $errorLogs = $logEntries |
        Where-Object LogLevel -eq Error |
        Where-Object {
            $match = $script:ExceptionRegex.Match($_.Message.Data)

            (-not $match) -or ($match.Groups[1].Value.Trim() -notin $IgnoreException)
        }

    if ($errorLogs)
    {
        $errorLogs | ForEach-Object { Write-Error "ERROR from PSES log: $($_.Message.Data)" }
    }
}

function CheckErrorResponse
{
    [CmdletBinding()]
    param(
        $Response
    )

    if (-not ($Response -is [PsesPsClient.LspErrorResponse]))
    {
        return
    }

    $msg = @"
Error Response Received
Code: $($Response.Code)
Message:
    $($Response.Message)

Data:
    $($Response.Data)
"@

    throw $msg
}

Describe "Loading and running PowerShellEditorServices" {
    BeforeAll {
        Import-Module -Force "$PSScriptRoot/../../module/PowerShellEditorServices"
        Import-Module -Force "$PSScriptRoot/../../src/PowerShellEditorServices.Engine/bin/Debug/netstandard2.0/publish/Omnisharp.Extensions.LanguageProtocol.dll"
        Import-Module -Force "$PSScriptRoot/../../tools/PsesPsClient/out/PsesPsClient"
        Import-Module -Force "$PSScriptRoot/../../tools/PsesLogAnalyzer"

        $logIdx = 0
        $psesServer = Start-PsesServer
        $client = Connect-PsesServer -InPipeName $psesServer.SessionDetails.languageServiceWritePipeName -OutPipeName $psesServer.SessionDetails.languageServiceReadPipeName
    }

    # This test MUST be first
    It "Starts and responds to an initialization request" {
        $request = Send-LspInitializeRequest -Client $client
        $response = Get-LspResponse -Client $client -Id $request.Id #-WaitMillis 99999
        $response.Id | Should -BeExactly $request.Id

        CheckErrorResponse -Response $response

        #ReportLogErrors -LogPath $psesServer.LogPath -FromIndex ([ref]$logIdx)
    }

    It "Can handle powerShell/getVersion request" {
        $request = Send-LspRequest -Client $client -Method "powerShell/getVersion"
        $response = Get-LspResponse -Client $client -Id $request.Id
        if ($IsCoreCLR) {
            $response.Result.edition | Should -Be "Core"
        } else {
            $response.Result.edition | Should -Be "Desktop"
        }
    }

    It "Can handle WorkspaceSymbol request" {
        $script = "
function Get-Foo {
    Write-Host 'hello'
}
"

        $file = Set-Content -Path (Join-Path $TestDrive "$([System.IO.Path]::GetRandomFileName()).ps1") -Value $script -PassThru -Force
        $request = Send-LspDidOpenTextDocumentRequest -Client $client `
            -Uri ([Uri]::new($file.PSPath).AbsoluteUri) `
            -Text ($file[0].ToString())

        # There's no response for this message, but we need to call Get-LspResponse
        # to increment the counter.
        Get-LspResponse -Client $client -Id $request.Id | Out-Null

        $request = Send-LspRequest -Client $client -Method "workspace/symbol" -Parameters @{
            query = ""
        }
        $response = Get-LspResponse -Client $client -Id $request.Id -WaitMillis 99999
        $response.Id | Should -BeExactly $request.Id

        $response.Result.Count | Should -Be 1
        $response.Result.name | Should -BeLike "Get-Foo*"
        CheckErrorResponse -Response $response

        # ReportLogErrors -LogPath $psesServer.LogPath -FromIndex ([ref]$logIdx)
    }

    It "Can get Diagnostics after opening a text document" {
        $script = '$a = 4'
        $file = Set-Content -Path (Join-Path $TestDrive "$([System.IO.Path]::GetRandomFileName()).ps1") -Value $script -PassThru -Force

        $request = Send-LspDidOpenTextDocumentRequest -Client $client `
            -Uri ([Uri]::new($file.PSPath).AbsoluteUri) `
            -Text ($file[0].ToString())

        # There's no response for this message, but we need to call Get-LspResponse
        # to increment the counter.
        Get-LspResponse -Client $client -Id $request.Id | Out-Null

        # Grab notifications for just the file opened in this test.
        $notifications = Get-LspNotification -Client $client | Where-Object {
            $_.Params.uri -match ([System.IO.Path]::GetFileName($file.PSPath))
        }

        $notifications | Should -Not -BeNullOrEmpty
        $notifications.Params.diagnostics | Should -Not -BeNullOrEmpty
        $notifications.Params.diagnostics.Count | Should -Be 1
        $notifications.Params.diagnostics.code | Should -Be "PSUseDeclaredVarsMoreThanAssignments"
    }

    It "Can get Diagnostics after changing settings" {
        $script = 'gci | % { $_ }'
        $file = Set-Content -Path (Join-Path $TestDrive "$([System.IO.Path]::GetRandomFileName()).ps1") -Value $script -PassThru -Force

        $request = Send-LspDidOpenTextDocumentRequest -Client $client `
            -Uri ([Uri]::new($file.PSPath).AbsoluteUri) `
            -Text ($file[0].ToString())

        # There's no response for this message, but we need to call Get-LspResponse
        # to increment the counter.
        Get-LspResponse -Client $client -Id $request.Id | Out-Null

        # Throw out any notifications from the first PSScriptAnalyzer run.
        Get-LspNotification -Client $client | Out-Null

        $request = Send-LspDidChangeConfigurationRequest -Client $client -Settings @{
            PowerShell = @{
                ScriptAnalysis = @{
                    Enable = $false
                }
            }
        }

        # Grab notifications for just the file opened in this test.
        $notifications = Get-LspNotification -Client $client | Where-Object {
            $_.Params.uri -match ([System.IO.Path]::GetFileName($file.PSPath))
        }
        $notifications | Should -Not -BeNullOrEmpty
        $notifications.Params.diagnostics | Should -BeNullOrEmpty
    }

    It "Can handle folding request" {
        $script = 'gci | % {
$_

@"
    $_
"@
}'

        $file = Set-Content -Path (Join-Path $TestDrive "$([System.IO.Path]::GetRandomFileName()).ps1") -Value $script -PassThru -Force

        $request = Send-LspDidOpenTextDocumentRequest -Client $client `
            -Uri ([Uri]::new($file.PSPath).AbsoluteUri) `
            -Text ($file[0].ToString())

        # There's no response for this message, but we need to call Get-LspResponse
        # to increment the counter.
        Get-LspResponse -Client $client -Id $request.Id | Out-Null

        # Throw out any notifications from the first PSScriptAnalyzer run.
        Get-LspNotification -Client $client | Out-Null



        $request = Send-LspRequest -Client $client -Method "textDocument/foldingRange" -Parameters ([Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.FoldingRangeParams] @{
            TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier] @{
                Uri = ([Uri]::new($file.PSPath).AbsoluteUri)
            }
        })

        $response = Get-LspResponse -Client $client -Id $request.Id

        $sortedResults = $response.Result | Sort-Object -Property startLine
        $sortedResults[0].startLine | Should -Be 0
        $sortedResults[0].startCharacter | Should -Be 8
        $sortedResults[0].endLine | Should -Be 5
        $sortedResults[0].endCharacter | Should -Be 1

        $sortedResults[1].startLine | Should -Be 3
        $sortedResults[1].startCharacter | Should -Be 0
        $sortedResults[1].endLine | Should -Be 4
        $sortedResults[1].endCharacter | Should -Be 2
    }

    # This test MUST be last
    It "Shuts down the process properly" {
        $request = Send-LspShutdownRequest -Client $client
        $response = Get-LspResponse -Client $client -Id $request.Id #-WaitMillis 99999
        $response.Id | Should -BeExactly $request.Id
        $response.Result | Should -BeNull

        CheckErrorResponse -Response $response

        # TODO: The server seems to stay up waiting for the debug connection
        # $psesServer.PsesProcess.HasExited | Should -BeTrue

        # We close the process here rather than in an AfterAll
        # since errors can occur and we want to test for them.
        # Naturally this depends on Pester executing tests in order.

        # We also have to dispose of everything properly,
        # which means we have to use these cascading try/finally statements
        try
        {
            $psesServer.PsesProcess.Kill()
        }
        finally
        {
            try
            {
                $psesServer.PsesProcess.Dispose()
            }
            finally
            {
                $client.Dispose()
                $client = $null
            }
        }

        #ReportLogErrors -LogPath $psesServer.LogPath -FromIndex ([ref]$logIdx)
    }

    AfterEach {
        if($client) {
            # Drain notifications
            Get-LspNotification -Client $client | Out-Null
        }
    }

    AfterAll {
        if ($psesServer.PsesProcess.HasExited -eq $false)
        {
            try
            {
                $psesServer.PsesProcess.Kill()
            }
            finally
            {
                try
                {
                    $psesServer.PsesProcess.Dispose()
                }
                finally
                {
                    $client.Dispose()
                }
            }
        }
    }
}
