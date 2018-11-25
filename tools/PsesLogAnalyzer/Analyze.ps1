function Get-PsesRpcMessageResponseTime {
    [CmdletBinding(DefaultParameterSetName="Path")]
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
            Where-Object MessageType -match Request |
            Foreach-Object { $requests[$_.Message.Id] = $_.Timestamp }

        $res = $logEntries |
            Where-Object MessageType -match Response |
            Foreach-Object {
                $elapsedMilliseconds = [int]($_.Timestamp - $requests[$_.Message.Id]).TotalMilliseconds
                [PsesLogEntryElapsed]::new($_, $elapsedMilliseconds)
            }

        $res
    }
}
