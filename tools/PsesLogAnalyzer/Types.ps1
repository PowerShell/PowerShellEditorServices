enum PsesLogLevel {
    Diagnostic;
    Verbose;
    Normal;
    Warning;
    Error;
}

enum PsesMessageType {
    Log;
    Request;
    Response;
    Notification;
}

enum PsesNotificationSource {
    Unknown;
    Client;
    Server;
}

class PsesLogMessage {
    [string]$Data

    PsesLogMessage([string]$Data) {
        $this.Data = $Data
    }

    [string] ToString() {
        $ofs = ''
        $ellipsis = if ($this.Data.Length -ge 100) { "..." } else { "" }
        return "$($this.Data[0..99])$ellipsis, DataSize: $($this.Data.Length)"
    }
}

class PsesJsonRpcMessage {
    [string]$Name
    [int]$Id
    [psobject]$Data
    [int]$DataSize

    PsesJsonRpcMessage([string]$Name, [int]$Id, [psobject]$Data, [int]$DataSize) {
        $this.Name = $Name
        $this.Id = $Id
        $this.Data = $Data
        $this.DataSize = $DataSize
    }

    [string] ToString() {
        return "Name: $($this.Name) Id: $($this.Id), DataSize: $($this.DataSize)"
    }
}

class PsesNotificationMessage {
    [string]$Name
    [PsesNotificationSource]$Source
    [psobject]$Data
    [int]$DataSize

    PsesNotificationMessage([string]$Name, [PsesNotificationSource]$Source, [psobject]$Data, [int]$DataSize) {
        $this.Name = $Name
        $this.Source = $Source
        $this.Data = $Data
        $this.DataSize = $DataSize
    }

    [string] ToString() {
        return "Name: $($this.Name) Source: $($this.Source), DataSize: $($this.DataSize)"
    }
}

class PsesLogEntry {
    [int]$Index
    [DateTime]$Timestamp
    [string]$TimestampStr
    [PsesLogLevel]$LogLevel
    [int]$ThreadId
    [string]$Method
    [string]$File
    [int]$LineNumber
    [PsesMessageType]$MessageType
    [psobject]$Message

    PsesLogEntry(
        [int]
        $Index,
        [DateTime]
        $Timestamp,
        [string]
        $TimestampStr,
        [PsesLogLevel]
        $LogLevel,
        [int]
        $ThreadId,
        [string]
        $Method,
        [string]
        $File,
        [int]
        $LineNumber,
        [PsesMessageType]
        $MessageType,
        [psobject]
        $Message) {

        $this.Index = $Index
        $this.Timestamp = $Timestamp
        $this.TimestampStr = $TimestampStr
        $this.LogLevel = $LogLevel
        $this.ThreadId = $ThreadId
        $this.Method = $Method
        $this.File = $File
        $this.LineNumber = $LineNumber
        $this.MessageType = $MessageType
        $this.Message = $Message
    }
}

class PsesLogEntryElapsed {
    [int]$Index
    [DateTime]$Timestamp
    [string]$TimestampStr
    [int]$ElapsedMilliseconds
    [PsesLogLevel]$LogLevel
    [int]$ThreadId
    [string]$Method
    [string]$File
    [int]$LineNumber
    [PsesMessageType]$MessageType
    [psobject]$Message

    PsesLogEntryElapsed([PsesLogEntry]$LogEntry, [int]$ElapsedMilliseconds) {
        $this.Index = $LogEntry.Index
        $this.Timestamp = $LogEntry.Timestamp
        $this.TimestampStr = $LogEntry.TimestampStr
        $this.LogLevel = $LogEntry.LogLevel
        $this.ThreadId = $LogEntry.ThreadId
        $this.Method = $LogEntry.Method
        $this.File = $LogEntry.File
        $this.LineNumber = $LogEntry.LineNumber
        $this.MessageType = $LogEntry.MessageType
        $this.Message = $LogEntry.Message

        $this.ElapsedMilliseconds = $ElapsedMilliseconds
    }
}
