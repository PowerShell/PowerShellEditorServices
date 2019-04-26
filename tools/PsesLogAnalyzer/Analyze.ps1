<#
.SYNOPSIS
    Gets LSP notification messages sent from either server to the client or vice-versa.
.DESCRIPTION
    Gets LSP notification messages sent from either server to the client or vice-versa.
.EXAMPLE
    C:\> Get-PsesRpcNotificationMessage $log
    Gets all LSP notification messages in the specified log.
.EXAMPLE
    C:\> Get-PsesRpcNotificationMessage $log -MessageName '$/cancelRequest'
    Gets all LSP $/cancelRequest notification messages in the specified log.
.EXAMPLE
    C:\> Get-PsesRpcNotificationMessage $log -Pattern powershell/.*
    Gets all LSP powershell notification messages in the specified log.
.INPUTS
    System.String or PsesLogEntry
.OUTPUTS
    PsesLogEntry
#>
function Get-PsesRpcNotificationMessage {
    [CmdletBinding(DefaultParameterSetName = "PsesLogEntry")]
    param(
        # Specifies a path to one or more PSES EditorServices log files.
        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "Path")]
        [Alias("PSPath")]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path,

        # Specifies PsesLogEntry objects to analyze.
        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "PsesLogEntry", ValueFromPipeline = $true)]
        [ValidateNotNull()]
        [psobject[]]
        $LogEntry,

        # Specifies a specific LSP notification.
        [Parameter(Position=1)]
        [ValidateSet(
            "$/cancelRequest",
            "initialized",
            "powerShell/executionStatusChanged",
            "textDocument/didChange",
            "textDocument/didClose",
            "textDocument/didOpen",
            "textDocument/didSave",
            "textDocument/publishDiagnostics",
            "workspace/didChangeConfiguration")]
        [string]
        $MessageName,

        # Specifies a regular expression pattern that filters the output based on the message name
        # e.g. 'cancelRequest'
        [Parameter()]
        [string]
        $Pattern,

        # Specifies a filter for either client or server sourced notifications.  By default both are output.
        [Parameter()]
        [ValidateSet('Client', 'Server')]
        [string]
        $Source
    )

    begin {
        if ($PSCmdlet.ParameterSetName -eq "Path") {
            $logEntries = Parse-PsesLog $Path
        }
    }

    process {
        if ($PSCmdlet.ParameterSetName -eq "PsesLogEntry") {
            $logEntries = $LogEntry
        }

        foreach ($entry in $logEntries) {
            if ($entry.LogMessageType -ne 'Notification') { continue }

            if ((!$MessageName -or ($entry.Message.Name -eq $MessageName)) -and
                (!$Pattern -or ($entry.Message.Name -match $Pattern)) -and
                (!$Source -or ($entry.Message.Source -eq $Source))) {

                $entry
            }
        }
    }
}

<#
.SYNOPSIS
    Outputs the response time for message LSP message.
.DESCRIPTION
    Outputs the response time for message LSP message.  Use the MessageNamePattern to
    limit the response time output to a specific message (or pattern of messages).
.EXAMPLE
    C:\> Get-PsesRpcMessageResponseTime $log
    Gets the response time of all LSP messages.
.EXAMPLE
    C:\> Get-PsesRpcMessageResponseTime $log -MessageName textDocument/foldingRange
    Gets the response time of all foldingRange LSP messages.
.EXAMPLE
    C:\> Get-PsesRpcMessageResponseTime $log -Pattern 'textDocument/.*Formatting'
    Gets the response time of all formatting LSP messages.
.INPUTS
    System.String or PsesLogEntry
.OUTPUTS
    PsesLogEntryElapsed
#>
function Get-PsesRpcMessageResponseTime {
    [CmdletBinding(DefaultParameterSetName = "PsesLogEntry")]
    param(
        # Specifies a path to one or more PSES EditorServices log files.
        [Parameter(Mandatory=$true, Position=0, ParameterSetName="Path")]
        [Alias("PSPath")]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path,

        # Specifies PsesLogEntry objects to analyze.
        [Parameter(Mandatory=$true, Position=0, ParameterSetName="PsesLogEntry", ValueFromPipeline=$true)]
        [ValidateNotNull()]
        [psobject[]]
        $LogEntry,

        # Specifies a specific LSP message for which to get response times.
        [Parameter(Position=1)]
        [ValidateSet(
            "textDocument/codeAction",
            "textDocument/codeLens",
            "textDocument/completion",
            "textDocument/documentSymbol",
            "textDocument/foldingRange",
            "textDocument/formatting",
            "textDocument/hover",
            "textDocument/rangeFormatting")]
        [string]
        $MessageName,

        # Specifies a regular expression pattern that filters the output based on the message name
        # e.g. 'textDocument/.*Formatting'
        [Parameter()]
        [string]
        $Pattern
    )

    begin {
        if ($PSCmdlet.ParameterSetName -eq "Path") {
            $logEntries = Parse-PsesLog $Path
        }
    }

    process {
        if ($PSCmdlet.ParameterSetName -eq "PsesLogEntry") {
            $logEntries += $LogEntry
        }
    }

    end {
        # Populate $requests hashtable with request timestamps
        $requests = @{}

        foreach ($entry in $logEntries) {
            if (($entry.LogMessageType -ne 'Request') -and ($entry.LogMessageType -ne 'Response')) { continue }

            if ((!$MessageName -or ($entry.Message.Name -eq $MessageName)) -and
                (!$Pattern -or ($entry.Message.Name -match $Pattern))) {

                $key = "$($entry.Message.Name)-$($entry.Message.Id)"
                if ($entry.LogMessageType -eq 'Request') {
                    $requests[$key] = $entry
                }
                else {
                    $request = $requests[$key]
                    if (!$request) {
                        Write-Warning "No corresponding request for response: $($entry.Message)"
                        continue
                    }

                    $elapsedMilliseconds = [int]($entry.Timestamp - $request.Timestamp).TotalMilliseconds
                    [PsesLogEntryElapsed]::new($entry, $elapsedMilliseconds)
                }
            }
        }
    }
}

function Get-PsesScriptAnalysisCompletionTime {
    [CmdletBinding(DefaultParameterSetName = "PsesLogEntry")]
    param(
        # Specifies a path to one or more PSES EditorServices log files.
        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "Path")]
        [Alias("PSPath")]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path,

        # Specifies PsesLogEntry objects to analyze.
        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "PsesLogEntry", ValueFromPipeline = $true)]
        [ValidateNotNull()]
        [psobject[]]
        $LogEntry
    )

    begin {
        if ($PSCmdlet.ParameterSetName -eq "Path") {
            $logEntries = Parse-PsesLog $Path
        }
    }

    process {
        if ($PSCmdlet.ParameterSetName -eq "PsesLogEntry") {
            $logEntries = $LogEntry
        }

        foreach ($entry in $logEntries) {
            if (($entry.LogMessageType -eq 'Log') -and ($entry.Message.Data -match '^\s*Script analysis of.*\[(?<ms>\d+)ms\]\s*$')) {
                $elapsedMilliseconds = [int]$matches["ms"]
                [PsesLogEntryElapsed]::new($entry, $elapsedMilliseconds)
            }
        }
    }
}

function Get-PsesIntelliSenseCompletionTime {
    [CmdletBinding(DefaultParameterSetName = "PsesLogEntry")]
    param(
        # Specifies a path to one or more PSES EditorServices log files.
        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "Path")]
        [Alias("PSPath")]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path,

        # Specifies PsesLogEntry objects to analyze.
        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "PsesLogEntry", ValueFromPipeline = $true)]
        [ValidateNotNull()]
        [psobject[]]
        $LogEntry
    )

    begin {
        if ($PSCmdlet.ParameterSetName -eq "Path") {
            $logEntries = Parse-PsesLog $Path
        }
    }

    process {
        if ($PSCmdlet.ParameterSetName -eq "PsesLogEntry") {
            $logEntries = $LogEntry
        }

        foreach ($entry in $logEntries) {
            # IntelliSense completed in 320ms.
            if (($entry.LogMessageType -eq 'Log') -and ($entry.Message.Data -match '^\s*IntelliSense completed in\s+(?<ms>\d+)ms.\s*$')) {
                $elapsedMilliseconds = [int]$matches["ms"]
                [PsesLogEntryElapsed]::new($entry, $elapsedMilliseconds)
            }
        }
    }
}

function Get-PsesMessage {
    [CmdletBinding(DefaultParameterSetName = "PsesLogEntry")]
    param(
        # Specifies a path to one or more PSES EditorServices log files.
        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "Path")]
        [Alias("PSPath")]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path,

        # Specifies PsesLogEntry objects to analyze.
        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = "PsesLogEntry", ValueFromPipeline = $true)]
        [ValidateNotNull()]
        [psobject[]]
        $LogEntry,

        # Specifies the log level entries to return.  Default returns Normal and above.
        # Use StrictMatch to return only the specified log level entries.
        [Parameter()]
        [PsesLogLevel]
        $LogLevel = $([PsesLogLevel]::Normal),

        # Use StrictMatch to return only the specified log level entries.
        [Parameter()]
        [switch]
        $StrictMatch
    )

    begin {
        if ($PSCmdlet.ParameterSetName -eq "Path") {
            $logEntries = Parse-PsesLog $Path
        }
    }

    process {
        if ($PSCmdlet.ParameterSetName -eq "PsesLogEntry") {
            $logEntries = $LogEntry
        }

        foreach ($entry in $logEntries) {
            if (($StrictMatch -and ($entry.LogLevel -eq $LogLevel)) -or
                (!$StrictMatch -and ($entry.LogLevel -ge $LogLevel))) {
                $entry
            }
        }
    }
}
