
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

Describe "Loading and running PowerShellEditorServices" {
    BeforeAll {
        Import-Module -Force "$PSScriptRoot/../../module/PowerShellEditorServices"
        Import-Module -Force "$PSScriptRoot/../../tools/PsesPsClient/out/PsesPsClient"
        Import-Module -Force "$PSScriptRoot/../../tools/PsesLogAnalyzer"

        $logIdx = 0
        $psesServer = Start-PsesServer
        $client = Connect-PsesServer -PipeName $psesServer.SessionDetails.languageServicePipeName
    }

    AfterAll {
        try
        {
            $psesServer.PsesProcess.Kill()
            $psesServer.PsesProcess.Dispose()
            $client.Dispose()
        }
        catch
        {
            # Do nothing
        }

        # TODO: We shouldn't need to skip this error.
        #       It's not clear why we get it but it only occurs on Windows
        ReportLogErrors -LogPath $psesServer.LogPath -FromIndex ([ref]$logIdx) #-IgnoreException 'EndOfStreamException'
    }

    It "Starts and responds to an initialization request" {
        $request = Send-LspInitializeRequest -Client $client
        $response = Get-LspResponse -Client $client -Id $request.Id
        $response.Id | Should -BeExactly $request.Id

        ReportLogErrors -LogPath $psesServer.LogPath -FromIndex ([ref]$logIdx)
    }

    It "Shuts down the process properly" {
        $request = Send-LspShutdownRequest -Client $client
        $response = Get-LspResponse -Client $client -Id $request.Id
        $response.Id | Should -BeExactly $request.Id
        $response.Result | Should -BeNull
        # TODO: The server seems to stay up waiting for the debug connection
        # $psesServer.PsesProcess.HasExited | Should -BeTrue

        ReportLogErrors -LogPath $psesServer.LogPath -FromIndex ([ref]$logIdx)
    }
}
