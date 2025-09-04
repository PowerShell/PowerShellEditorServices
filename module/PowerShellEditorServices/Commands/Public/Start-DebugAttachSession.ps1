# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

using namespace System.Collections
using namespace System.Management.Automation
using namespace System.Reflection
using namespace System.Threading
using namespace System.Threading.Tasks

function Start-DebugAttachSession {
    <#
    .EXTERNALHELP ..\PowerShellEditorServices.Commands-help.xml
    #>
    [OutputType([System.Management.Automation.Job2])]
    [CmdletBinding(DefaultParameterSetName = 'ProcessId')]
    param(
        [Parameter()]
        [string]
        $Name,

        [Parameter(ParameterSetName = 'ProcessId')]
        [int]
        $ProcessId,

        [Parameter(ParameterSetName = 'CustomPipeName')]
        [string]
        $CustomPipeName,

        [Parameter()]
        [string]
        $RunspaceName,

        [Parameter()]
        [int]
        $RunspaceId,

        [Parameter()]
        [string]
        $ComputerName,

        [Parameter()]
        [ValidateSet('Close', 'Hide', 'Keep')]
        [string]
        $WindowActionOnEnd,

        [Parameter()]
        [IDictionary[]]
        $PathMapping,

        [Parameter()]
        [switch]
        $AsJob
    )

    $ErrorActionPreference = 'Stop'

    try {
        if ($PSBoundParameters.ContainsKey('RunspaceId') -and $RunspaceName) {
            $err = [ErrorRecord]::new(
                [ArgumentException]::new("Cannot specify both RunspaceId and RunspaceName parameters"),
                "InvalidRunspaceParameters",
                [ErrorCategory]::InvalidArgument,
                $null)
            $err.ErrorDetails = [ErrorDetails]::new("")
            $err.ErrorDetails.RecommendedAction = 'Specify only one of RunspaceId or RunspaceName.'
            $PSCmdlet.WriteError($err)
            return
        }

        # Var will be set by PSES in configurationDone before launching script
        $debugServer = Get-Variable -Name __psEditorServices_DebugServer -ValueOnly -ErrorAction Ignore
        if (-not $debugServer) {
            $err = [ErrorRecord]::new(
                [Exception]::new("Cannot start a new attach debug session unless running in an existing launch debug session not in a temporary console"),
                "NoDebugSession",
                [ErrorCategory]::InvalidOperation,
                $null)
            $err.ErrorDetails = [ErrorDetails]::new("")
            $err.ErrorDetails.RecommendedAction = 'Launch script with debugging to ensure the debug session is available.'
            $PSCmdlet.WriteError($err)
            return
        }

        if ($AsJob -and -not (Get-Command -Name Start-ThreadJob -ErrorAction Ignore)) {
            $err = [ErrorRecord]::new(
                [Exception]::new("Cannot use the -AsJob parameter unless running on PowerShell 7+ or the ThreadJob module is present"),
                "NoThreadJob",
                [ErrorCategory]::InvalidArgument,
                $null)
            $err.ErrorDetails = [ErrorDetails]::new("")
            $err.ErrorDetails.RecommendedAction = 'Install the ThreadJob module or run on PowerShell 7+.'
            $PSCmdlet.WriteError($err)
            return
        }

        $configuration = @{
            type = 'PowerShell'
            request = 'attach'
            # A temp console is also needed as the current one is busy running
            # this code. Failing to set this will cause a deadlock.
            createTemporaryIntegratedConsole = $true
        }

        if ($ProcessId) {
            if ($ProcessId -eq $PID) {
                $err = [ErrorRecord]::new(
                    [ArgumentException]::new("PSES does not support attaching to the current editor process"),
                    "AttachToCurrentProcess",
                    [ErrorCategory]::InvalidArgument,
                    $PID)
                $err.ErrorDetails = [ErrorDetails]::new("")
                $err.ErrorDetails.RecommendedAction = 'Specify a different process id.'
                $PSCmdlet.WriteError($err)
                return
            }

            if ($Name) {
                $configuration.name = $Name
            }
            else {
                $configuration.name = "Attach Process $ProcessId"
            }
            $configuration.processId = $ProcessId
        }
        elseif ($CustomPipeName) {
            if ($Name) {
                $configuration.name = $Name
            }
            else {
                $configuration.name = "Attach Pipe $CustomPipeName"
            }
            $configuration.customPipeName = $CustomPipeName
        }
        else {
            $configuration.name = 'Attach Session'
        }

        if ($ComputerName) {
            $configuration.computerName = $ComputerName
        }

        if ($PSBoundParameters.ContainsKey('RunspaceId')) {
            $configuration.runspaceId = $RunspaceId
        }
        elseif ($RunspaceName) {
            $configuration.runspaceName = $RunspaceName
        }

        if ($WindowActionOnEnd) {
            $configuration.temporaryConsoleWindowActionOnDebugEnd = $WindowActionOnEnd.ToLowerInvariant()
        }

        if ($PathMapping) {
            $configuration.pathMappings = $PathMapping
        }

        # https://microsoft.github.io/debug-adapter-protocol/specification#Reverse_Requests_StartDebugging
        $resp = $debugServer.SendRequest(
            'startDebugging',
            @{
                configuration = $configuration
                request = 'attach'
            }
        )

        # PipelineStopToken added in pwsh 7.6
        $cancelToken = if ($PSCmdlet.PipelineStopToken) {
            $PSCmdlet.PipelineStopToken
        }
        else {
            [CancellationToken]::new($false)
        }

        # There is no response for a startDebugging request
        $task = $resp.ReturningVoid($cancelToken)

        $waitTask = {
            [CmdletBinding()]
            param ([Parameter(Mandatory)][Task]$Task)

            while (-not $Task.AsyncWaitHandle.WaitOne(300)) {}
            $null = $Task.GetAwaiter().GetResult()
        }

        if ($AsJob) {
            # Using the Ast to build the scriptblock allows the job to inherit
            # the using namespace entries and include the proper line/script
            # paths in any error traces that are emitted.
            Start-ThreadJob -ScriptBlock {
                & ($args[0]).Ast.GetScriptBlock() $args[1]
            } -ArgumentList $waitTask, $task
        }
        else {
            & $waitTask $task
        }
    }
    catch {
        $PSCmdlet.WriteError($_)
        return
    }
}