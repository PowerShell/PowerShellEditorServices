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
            if ($entry.LogMessageType -eq 'Notification') {
                if (!$Source -or ($entry.Message.Source -eq $Source)) {
                    $entry
                }
            }
        }
    }
}

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
        $LogEntry
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
        $logEntries |
            Where-Object LogMessageType -match Request |
            Foreach-Object { $requests[$_.Message.Id] = $_.Timestamp }

        $res = $logEntries |
            Where-Object LogMessageType -match Response |
            Foreach-Object {
                $elapsedMilliseconds = [int]($_.Timestamp - $requests[$_.Message.Id]).TotalMilliseconds
                [PsesLogEntryElapsed]::new($_, $elapsedMilliseconds)
            }

        $res
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
