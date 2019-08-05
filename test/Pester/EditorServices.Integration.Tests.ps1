
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

function New-TestFile
{
    param(
        [Parameter(Mandatory)]
        [string]
        $Script,

        [Parameter()]
        [string]
        $FileName = "$([System.IO.Path]::GetRandomFileName()).ps1"
    )

    $file = Set-Content -Path (Join-Path $TestDrive $FileName) -Value $Script -PassThru -Force

    $request = Send-LspDidOpenTextDocumentRequest -Client $client `
        -Uri ([Uri]::new($file.PSPath).AbsoluteUri) `
        -Text ($file[0].ToString())

    # To give PSScriptAnalyzer a chance to run.
    Start-Sleep 1

    # There's no response for this message, but we need to call Get-LspResponse
    # to increment the counter.
    Get-LspResponse -Client $client -Id $request.Id | Out-Null

    # Throw out any notifications from the first PSScriptAnalyzer run.
    Get-LspNotification -Client $client | Out-Null

    $file.PSPath
}

Describe "Loading and running PowerShellEditorServices" {
    BeforeAll {
        Import-Module -Force "$PSScriptRoot/../../module/PowerShellEditorServices"
        Import-Module -Force (Resolve-Path "$PSScriptRoot/../../src/PowerShellEditorServices.Engine/bin/*/netstandard2.0/publish/Omnisharp.Extensions.LanguageProtocol.dll")
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
        New-TestFile -Script "
function Get-Foo {
    Write-Host 'hello'
}
"

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
        $file = New-TestFile -Script 'gci | % { $_ }'

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
        $filePath = New-TestFile -Script 'gci | % {
$_

@"
    $_
"@
}'

        $request = Send-LspRequest -Client $client -Method "textDocument/foldingRange" -Parameters ([Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.FoldingRangeParams] @{
            TextDocument = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.TextDocumentIdentifier] @{
                Uri = ([Uri]::new($filePath).AbsoluteUri)
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

    It "can handle a normal formatting request" {
        $filePath = New-TestFile -Script '
gci | % {
Get-Process
}

'

        $request = Send-LspFormattingRequest -Client $client `
            -Uri ([Uri]::new($filePath).AbsoluteUri)

        $response = Get-LspResponse -Client $client -Id $request.Id

        # If we have a tab, formatting ran.
        $response.Result.newText.Contains("`t") | Should -BeTrue -Because "We expect a tab."
    }

    It "can handle a range formatting request" {
        $filePath = New-TestFile -Script '
gci | % {
Get-Process
}

'

        $range = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Range]@{
            Start = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
                Line = 2
                Character = 0
            }
            End  = [Microsoft.PowerShell.EditorServices.Protocol.LanguageServer.Position]@{
                Line = 3
                Character = 0
            }
        }

        $request = Send-LspRangeFormattingRequest -Client $client `
            -Uri ([Uri]::new($filePath).AbsoluteUri) `
            -Range $range

        $response = Get-LspResponse -Client $client -Id $request.Id

        # If we have a tab, formatting ran.
        $response.Result.newText.Contains("`t") | Should -BeTrue -Because "We expect a tab."
    }

    It "Can handle a textDocument/documentSymbol request" {
        $filePath = New-TestFile -Script '
function Get-Foo {

}

Get-Foo
'

        $request = Send-LspDocumentSymbolRequest -Client $client `
            -Uri ([Uri]::new($filePath).AbsoluteUri)

        $response = Get-LspResponse -Client $client -Id $request.Id

        $response.Result.location.range.start.line | Should -BeExactly 1
        $response.Result.location.range.start.character | Should -BeExactly 0
        $response.Result.location.range.end.line | Should -BeExactly 3
        $response.Result.location.range.end.character | Should -BeExactly 1
    }

    It "Can handle a textDocument/references request" {
        $filePath = New-TestFile -Script '
function Get-Bar {

}

Get-Bar
'

        $request = Send-LspReferencesRequest -Client $client `
            -Uri ([Uri]::new($filePath).AbsoluteUri) `
            -LineNumber 5 `
            -CharacterNumber 0

        $response = Get-LspResponse -Client $client -Id $request.Id

        $response.Result.Count | Should -BeExactly 2
        $response.Result[0].range.start.line | Should -BeExactly 1
        $response.Result[0].range.start.character | Should -BeExactly 9
        $response.Result[0].range.end.line | Should -BeExactly 1
        $response.Result[0].range.end.character | Should -BeExactly 16
        $response.Result[1].range.start.line | Should -BeExactly 5
        $response.Result[1].range.start.character | Should -BeExactly 0
        $response.Result[1].range.end.line | Should -BeExactly 5
        $response.Result[1].range.end.character | Should -BeExactly 7
    }

    It "Can handle a textDocument/documentHighlight request" {
        $filePath = New-TestFile -Script @'
Write-Host 'Hello!'

Write-Host 'Goodbye'
'@

        $documentHighlightParams = @{
            Client = $client
            Uri = ([uri]::new($filePath).AbsoluteUri)
            LineNumber = 3
            CharacterNumber = 1
        }
        $request = Send-LspDocumentHighlightRequest @documentHighlightParams

        $response = Get-LspResponse -Client $client -Id $request.Id

        $response.Result.Count | Should -BeExactly 2
        $response.Result[0].Range.Start.Line | Should -BeExactly 0
        $response.Result[0].Range.Start.Character | Should -BeExactly 0
        $response.Result[0].Range.End.Line | Should -BeExactly 0
        $response.Result[0].Range.End.Character | Should -BeExactly 10
        $response.Result[1].Range.Start.Line | Should -BeExactly 2
        $response.Result[1].Range.Start.Character | Should -BeExactly 0
        $response.Result[1].Range.End.Line | Should -BeExactly 2
        $response.Result[1].Range.End.Character | Should -BeExactly 10
    }

    It "Can handle a powerShell/getPSHostProcesses request" {
        $request = Send-LspRequest -Client $client -Method "powerShell/getPSHostProcesses"
        $response = Get-LspResponse -Client $client -Id $request.Id
        $response.Result | Should -Not -BeNullOrEmpty

        $processInfos = @(Get-PSHostProcessInfo)

        # We need to subtract one because this message fiilters out the "current" process.
        $processInfos.Count - 1 | Should -BeExactly $response.Result.Count

        $response.Result[0].processName |
            Should -MatchExactly -RegularExpression "((pwsh)|(powershell))(.exe)*"
    }

    It "Can handle a powerShell/getRunspace request" {
        $processInfos = Get-PSHostProcessInfo

        $request = Send-LspGetRunspaceRequest -Client $client -ProcessId $processInfos[0].ProcessId
        $response = Get-LspResponse -Client $client -Id $request.Id

        $response.Result | Should -Not -BeNullOrEmpty
        $response.Result.Count | Should -BeGreaterThan 0
    }

    It "Can handle a textDocument/codeLens Pester request" {
        $filePath = New-TestFile -FileName ("$([System.IO.Path]::GetRandomFileName()).Tests.ps1") -Script '
Describe "DescribeName" {
    Context "ContextName" {
        It "ItName" {
            1 | Should -Be 1
        }
    }
}
'

        $request = Send-LspCodeLensRequest -Client $client `
            -Uri ([Uri]::new($filePath).AbsoluteUri)

        $response = Get-LspResponse -Client $client -Id $request.Id
        $response.Result.Count | Should -BeExactly 2

        # Both commands will have the same values for these so we can check them like so.
        $response.Result.range.start.line | Should -Be @(1, 1)
        $response.Result.range.start.character | Should -Be @(0, 0)
        $response.Result.range.end.line | Should -Be @(7, 7)
        $response.Result.range.end.character | Should -Be @(1, 1)

        $response.Result.command.title[0] | Should -Be "Run tests"
        $response.Result.command.title[1] | Should -Be "Debug tests"
    }

    It "Can handle a textDocument/codeLens and codeLens/resolve References request" {
        $filePath = New-TestFile -Script '
function Get-Foo {

}

Get-Foo
'

        $request = Send-LspCodeLensRequest -Client $client `
            -Uri ([Uri]::new($filePath).AbsoluteUri)

        $response = Get-LspResponse -Client $client -Id $request.Id
        $response.Result.Count | Should -BeExactly 1
        $response.Result.data.data.ProviderId | Should -Be ReferencesCodeLensProvider
        $response.Result.range.start.line | Should -BeExactly 1
        $response.Result.range.start.character | Should -BeExactly 0
        $response.Result.range.end.line | Should -BeExactly 3
        $response.Result.range.end.character | Should -BeExactly 1

        $request = Send-LspCodeLensResolveRequest -Client $client -CodeLens $response.Result[0]
        $response = Get-LspResponse -Client $client -Id $request.Id

        $response.Result.command.title | Should -Be '1 reference'
        $response.Result.command.command | Should -Be 'editor.action.showReferences'
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
