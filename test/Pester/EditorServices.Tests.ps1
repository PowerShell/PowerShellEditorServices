Describe "Loading and running PowerShellEditorServices" {
    BeforeAll {
        Import-Module "$PSScriptRoot/../../tools/PsesPsClient"

        $psesServer = Start-PsesServer
        $pipe = Connect-NamedPipe -PipeName $psesServer.SessionDetails.languageServicePipeName
    }

    AfterAll {
        $pipe.Dispose()
        $psesServer.Process.Close()
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
        $response.Result.Type | Should -Be 'Null'
    }
}
