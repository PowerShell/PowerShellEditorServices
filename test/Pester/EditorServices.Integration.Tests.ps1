
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
            }
        }

        #ReportLogErrors -LogPath $psesServer.LogPath -FromIndex ([ref]$logIdx)
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
