---
external help file: PowerShellEditorServices.Commands-help.xml
Module Name: PowerShellEditorServices.Commands
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/main/module/docs/Start-DebugAttachSession.md
schema: 2.0.0
---

# Start-DebugAttachSession

## SYNOPSIS

Starts a new debug session attached to the specified PowerShell instance.

## SYNTAX

### ProcessId (Default)
```
Start-DebugAttachSession [-Name <String>] [-ProcessId <Int32>] [-RunspaceName <String>] [-RunspaceId <Int32>]
 [-ComputerName <String>] [-WindowActionOnEnd {Close | Hide | Keep}] [-PathMapping <IDictionary[]>] [-AsJob]
 [<CommonParameters>]
```

### CustomPipeName
```
Start-DebugAttachSession [-Name <String>] [-CustomPipeName <String>] [-RunspaceName <String>]
 [-RunspaceId <Int32>] [-ComputerName <String>] [-WindowActionOnEnd {Close | Hide | Keep}]
 [-PathMapping <IDictionary[]>] [-AsJob] [<CommonParameters>]
```

## DESCRIPTION

The Start-DebugAttachSession function can be used to start a new debug session that is attached to the specified PowerShell instance. The caller must be running in an existing launched debug session, the launched session is not running in a temporary console, and the launched session is not entered into a remote PSSession. If the callers script ends before the new debug session is completed, the debug session for the child will also end.

The function will return once the attach response was received by the debug server. For an example, an attach request will return once PowerShell has attached to the process and has called `Debug-Runspace`. If you need to return early use the `-AsJob` parameter to return a `Job` object immediately that can be used to wait for the response at a later time.

If `-ProcessId` or `-CustomPipeName` is not specified, the debug client will prompt for process to connect to. If `-RunspaceId` or `-RunspaceName` is not specified, the debug client will prompt for which runspace to connect to.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
$pipeName = "TestPipe-$(New-Guid)"
$procParams = @{
    FilePath = 'pwsh'
    ArgumentList = ('-CustomPipeName {0} -File other-script.ps1' -f $pipeName)
    PassThru = $true
}
$proc = Start-Process @procParams

Start-DebugAttachSession -CustomPipeName $pipeName -RunspaceId 1
$proc | Wait-Process


<# The contents of `other-script.ps1` is #>
# Waits until PowerShell has attached
$runspaces = Get-Runspace
while ($true) {
    if (Get-Runspace | Where-Object { $_.Id -notin $runspaces.Id }) {
        break
    }
    Start-Sleep -Seconds 1
}

# WinPS will only have breakpoints synced once the debugger has been hit.
if ($PSVersionTable.PSVersion -lt '6.0') {
    Wait-Debugger
}

# Place breakpoint below or use Wait-Debugger
# to have the attach debug session break.
$a = 'abc'
$b = ''
Write-Host "Test $a - $PID"
```

Launches a new PowerShell process with a custom pipe and starts a new attach configuration that will debug the new process under a child debugging session. The caller waits until the new process ends before ending the parent session.

### -------------------------- EXAMPLE 2 --------------------------

```powershell
$attachParams = @{
    ComputerName = 'remote-windows'
    ProcessId = $remotePid
    RunspaceId = 1
    PathMapping = @(
        @{
            localRoot = 'C:\local\path\to\scripts\'
            remoteRoot = 'C:\remote\path\on\remote-windows\'
        }
    )
}
Start-DebugAttachSession @attachParams
```

Attaches to a remote PSSession through the WSMan parameter and maps the remote path running the script in the PSSession to the same copy of files locally. For example `remote-windows` is running the script `C:\remote\path\on\remote-windows\script.ps1` but the same script(s) are located locally on the current host `C:\local\path\to\scripts\script.ps1`.

The debug client can see the remote files as local when setting breakpoints and inspecting the callstack with this mapped path.

## PARAMETERS

### -AsJob

Instead of waiting for the start debugging response before returning, the `-AsJob` parameter will output a job immediately after sending the request that waits for the job. This is useful if further work is needed for a debug session to successfully attach and start debugging the target runspace.

This is only supported when the calling script is running on PowerShell 7+ or the `ThreadJob` module is present.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ComputerName

The computer name to which a remote session will be established before attaching to the target runspace. If specified, the temporary console will run `Enter-PSSession -ComputerName ...` to connect to a host over WSMan before attaching to the requested PowerShell instance.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CustomPipeName

The custom pipe name of the PowerShell host process to attach to. This option is mutually exclusive with `-ProcessId`.

```yaml
Type: String
Parameter Sets: CustomPipeName
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name

The name of the debug session to show in the debug client.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PathMapping

An array of dictionaries with the keys `localRoot` and `remoteRoot` that maps a local and remote path root to each other. This option is useful when attaching to a PSSession running a script that is not accessible locally but can be found under a different path.

It is a good idea to ensure the `localRoot` and `remoteRoot` entries are either the absolute path to a script or ends with the trailing directory separator if specifying a directory. A path can also be mapped from a Windows and non-Windows path, just ensure the correct directory separators are used for each OS type. For example `/` for non-Windows and `\` for Windows.

```yaml
Type: IDictionary[]
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProcessId

The ID of the PowerShell host process that should be attached. This option is mutually exclusive with `-CustomPipeName`.

```yaml
Type: Int32
Parameter Sets: ProcessId
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RunspaceId

The ID of the runspace to debug in the attached process. This option is mutually exclusive with `-RunspaceName`.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RunspaceName

The name of the runspace to debug in the attached process. This option is mutually exclusive with `-RunspaceId`.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -WindowActionOnEnd

Specifies the action to take on the temporary debug console created by the debug client after the attached session ends. This corresponds to the VSCode attach configuration option `temporaryConsoleWindowActionOnDebugEnd`. Setting to `Close` will close the debug console, `Hide` will move back to the last debug console before the attach session started, and `Keep` (default) will keep the active terminal as the attached session.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

You can't pipe objects to this function.

## OUTPUTS

### None

By default, this function returns no output.

### System.Management.Automation.Job2

When you use the `-AsJob` parameter, this function returns the `Job` object that is waiting for the response.

## NOTES

The function will fail if the caller is not running under a debug session or was started through an attach request.

## RELATED LINKS
