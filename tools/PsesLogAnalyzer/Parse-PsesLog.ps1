
$peekBuf = $null
$currentLineNum = 1

function Parse-PsesLog {
    param(
        # Specifies a path to one or more PSES EditorServices log files.
        [Parameter(Mandatory=$true, Position=0)]
        [Alias("PSPath")]
        [ValidateNotNullOrEmpty()]
        [string]
        $Path
    )

    begin {

        # Example log entry start:
        # 2018-11-24 12:26:58.302 [DIAGNOSTIC] tid:28 in 'ReadMessage' C:\Users\Keith\GitHub\rkeithhill\PowerShellEditorServices\src\PowerShellEditorServices.Protocol\MessageProtocol\MessageReader.cs:114:
        $logEntryRegex = 
            [regex]::new(
                '(?<ts>[^\[]+)\[(?<lev>([^\]]+))\]\s+tid:(?<tid>\d+)\s+in\s+''(?<meth>\w+)''\s+(?<file>..[^:]+):(?<line>\d+)', 
                [System.Text.RegularExpressions.RegexOptions]::Compiled -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

        $filestream = 
            [System.IO.FileStream]::new(
                $Path, 
                [System.IO.FileMode]:: Open, 
                [System.IO.FileAccess]::Read, 
                [System.IO.FileShare]::ReadWrite, 
                4096,
                [System.IO.FileOptions]::SequentialScan)

        $streamReader = [System.IO.StreamReader]::new($filestream, [System.Text.Encoding]::UTF8)

        function nextLine() {
            if ($null -ne $peekBuf) {
                $line = $peekBuf
                $script:peekBuf = $null
            }
            else {
                $line = $streamReader.ReadLine()
            }

            $script:currentLineNum += 1
            $line
        }

        function peekLine() {
            if ($null -ne $peekBuf) {
                $line = $peekBuf;
            }
            else {
                $line = $script:peekBuf = $streamReader.ReadLine()
            }

            $line
        }
            
        function parseLogEntryStart([string]$line) {
            while ($line -notmatch $logEntryRegex) {
                Write-Warning "Ignoring line: '$line'"
                $line = nextLine
            }
            
            [string]$timestampStr = $matches["ts"]
            [DateTime]$timestamp = $timestampStr
            [PsesLogLevel]$logLevel = $matches["lev"]
            [int]$threadId = $matches["tid"]
            [string]$method = $matches["meth"]
            [string]$file = $matches["file"]
            [int]$lineNumber = $matches["line"]
 
            $message = parseMessage $method

            [PsesLogEntry]::new($timestamp, $timestampStr, $logLevel, $threadId, $method, $file, $lineNumber, 
                $message.MessageType, $message.Message)
        }

        function parseMessage([string]$Method) {
            $result = [PSCustomObject]@{
                MessageType = [PsesMessageType]::Log
                Message = $null
            }

            $line = nextLine
            if ($null -eq $line) {
                Write-Warning "$($MyInvocation.MyCommand.Name) encountered end of file early."
                return $result
            }

            if (($Method -eq 'ReadMessage') -and 
                ($line -match '\s+Received Request ''(?<msg>[^'']+)'' with id (?<id>\d+)')) {
                $result.MessageType = [PsesMessageType]::Request
                $msg = $matches["msg"]
                $id = $matches["id"]
                $json = parseJsonMessageBody
                $result.Message = [PsesJsonRpcMessage]::new($msg, $id, $json)
            }
            elseif (($Method -eq 'ReadMessage') -and 
                    ($line -match '\s+Received event ''(?<msg>[^'']+)''')) {
                $result.MessageType = [PsesMessageType]::Notification
                $msg = $matches["msg"]
                $json = parseJsonMessageBody
                $result.Message = [PsesNotificationMessage]::new($msg, [PsesNotificationSource]::Client, $json)
            }
            elseif (($Method -eq 'WriteMessage') -and 
                    ($line -match '\s+Writing Response ''(?<msg>[^'']+)'' with id (?<id>\d+)')) {
                $result.MessageType = [PsesMessageType]::Response
                $msg = $matches["msg"]
                $id = $matches["id"]
                $json = parseJsonMessageBody
                $result.Message = [PsesJsonRpcMessage]::new($msg, $id, $json)
            }
            elseif (($Method -eq 'WriteMessage') -and 
                    ($line -match '\s+Writing event ''(?<msg>[^'']+)''')) {
                $result.MessageType = [PsesMessageType]::Notification
                $msg = $matches["msg"]
                $json = parseJsonMessageBody
                $result.Message = [PsesNotificationMessage]::new($msg, [PsesNotificationSource]::Server, $json)
            }
            else {
                $result.MessageType = [PsesMessageType]::Log
                $body = parseMessageBody $line
                $result.Message = [PsesLogMessage]::new($body)
            }

            $result
        }

        function parseMessageBody([string]$startLine = '') {
            $result = $startLine
            try {
                while ($true) {
                    $peekLine = peekLine
                    if ($null -eq $peekLine) {
                        break
                    }

                    if ($peekLine -match $logEntryRegex) {
                        break
                    }

                    $result += (nextLine) + "`r`n"
                }

            }
            catch {
                Write-Error "Failed parsing message body with error: $_"
            }

            $result.Trim()
        }

        function parseJsonMessageBody() {
            $obj = $null

            try {
                $result = parseMessageBody
                $obj = $result.Trim() | ConvertFrom-Json
            }
            catch {
                Write-Error "Failed parsing JSON message body with error: $_"
            }

            $obj
        }
    }

    process {
        while ($null -ne ($line = nextLine)) {
            parseLogEntryStart $line
        }
    }

    end {
        if ($streamReader) { $streamReader.Dispose() }
    }
}
