Describe "Loading and running PowerShellEditorServices" {
    BeforeAll {
        Import-Module "$PSScriptRoot/../../tools/PsesPsClient"

        $psesServer = Start-PsesServer
        $pipe = Connect-NamedPipe -PipeName $psesServer.SessionDetails.languageServicePipeName
    }

    AfterAll {
        $pipe.Dispose()
        $psesServer.PsesProcess.Kill()
        $psesServer.PsesProcess.Dispose()
    }

    It "Starts and responds to an initialization request" {
        $request = Send-LspInitializeRequest -Pipe $pipe
        $response = $null
        $pipe.TryGetNextResponse([ref]$response, 5000) | Should -BeTrue
        $response.Id | Should -BeExactly $request.Id
    }

    It "Shuts down the process properly" {
        $request = Send-LspShutdownRequest -Pipe $pipe
        $response = $null
        $pipe.TryGetNextResponse([ref]$response, 5000) | Should -BeTrue
        $response.Id | Should -BeExactly $request.Id
        $response.Result | Should -BeNull
        # TODO: The server stays up waiting for the debug connection
        # $psesServer.PsesProcess.HasExited | Should -BeTrue
    }
}
