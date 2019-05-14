Describe "Loading and running PowerShellEditorServices" {
    BeforeAll {
        Import-Module -Force "$PSScriptRoot/../../module/PowerShellEditorServices"
        Import-Module -Force "$PSScriptRoot/../../tools/PsesPsClient/out/PsesPsClient"

        $stderrFile = [System.IO.Path]::GetTempFileName()

        $psesServer = Start-PsesServer -StderrFile $stderrFile
        $client = Connect-PsesServer -PipeName $psesServer.SessionDetails.languageServicePipeName
    }

    AfterAll {
        if (Test-Path $stderrFile)
        {
            $errorMessages = Get-Content -Raw $stderrFile
            if ($errorMessages)
            {
                Write-Error $errorMessages
            }
        }

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
