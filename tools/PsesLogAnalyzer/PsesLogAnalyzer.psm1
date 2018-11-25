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
        return "$($this.Data[0..100])"
    }    
}

class PsesJsonRpcMessage {
    [string]$Name
    [int]$Id
    [psobject]$Data

    PsesJsonRpcMessage([string]$Name, [int]$Id, [psobject]$Data) {
        $this.Name = $Name
        $this.Id = $Id
        $this.Data = $Data
    }

    [string] ToString() {
        return "Name: $($this.Name) Id: $($this.Id)"
    }
}

class PsesNotificationMessage {
    [string]$Name
    [PsesNotificationSource]$Source
    [psobject]$Data

    PsesNotificationMessage([string]$Name, [PsesNotificationSource]$Source, [psobject]$Data) {
        $this.Name = $Name
        $this.Source = $Source
        $this.Data = $Data
    }

    [string] ToString() {
        return "Name: $($this.Name) Source: $($this.Source)"
    }    
}

class PsesLogEntry {
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

. $PSScriptRoot\Parse-PsesLog.ps1

Export-ModuleMember -Function *-*
