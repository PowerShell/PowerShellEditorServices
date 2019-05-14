Describe "Loading and running PowerShellEditorServices" {
    BeforeAll {
        Import-Module -Force "$PSScriptRoot/../../module/PowerShellEditorServices"
        Import-Module -Force "$PSScriptRoot/../../tools/PsesPsClient/out/PsesPsClient"
        Import-Module -Force "$PSScriptRoot/../../tools/PsesLogAnalyzer"

        $stderrFile = [System.IO.Path]::GetTempFileName()

        $logPath = Join-Path ([System.IO.Path]::GetTempPath()) 'PSES_IntegrationTest.log'

        $psesServer = Start-PsesServer -ErrorFile $stderrFile -LogPath $logPath
        $client = Connect-PsesServer -PipeName $psesServer.SessionDetails.languageServicePipeName
    }

    AfterAll {
        try
        {
            $client.Dispose()
            $psesServer.PsesProcess.Kill()
            $psesServer.PsesProcess.Dispose()
        }
        catch
        {
            # Do nothing
        }

        $errorLogs = Parse-PsesLog $logPath |
            Where-Object LogLevel -eq Error

        if ($errorLogs)
        {
            $errorLogs | ForEach-Object { Write-Error $_.Message.Data }
            throw "Error found in logs post execution"
        }
    }

    It "Starts and responds to an initialization request" {
        $request = Send-LspInitializeRequest -Client $client
        $response = Get-LspResponse -Client $client -Id $request.Id
        $response.Id | Should -BeExactly $request.Id
    }

    It "Shuts down the process properly" {
        $request = Send-LspShutdownRequest -Client $client
        $response = Get-LspResponse -Client $client -Id $request.Id
        $response.Id | Should -BeExactly $request.Id
        $response.Result | Should -BeNull
        # TODO: The server seems to stay up waiting for the debug connection
        # $psesServer.PsesProcess.HasExited | Should -BeTrue
    }
}
