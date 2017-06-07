---
external help file: PowerShellEditorServices.Commands-help.xml
online version:
schema: 2.0.0
---

# Import-EditorCommand

## SYNOPSIS

Imports commands with the PSEditorCommand attribute into PowerShell Editor Services.

## SYNTAX

### ByModule

```powershell
Import-EditorCommand [-Module] <PSModuleInfo[]> [-Force] [-PassThru] [<CommonParameters>]
```

### ByCommand

```powershell
Import-EditorCommand [-Command] <CommandInfo[]> [-Force] [-PassThru] [<CommonParameters>]
```

## DESCRIPTION

The Import-EditorCommand function will search the specified module for functions tagged as editor commands and register them with PowerShell Editor Services. By default, if a module is specified only exported functions will be processed.

Alternatively, you can specify command info objects (like those from the Get-Command cmdlet) to be processed directly.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
Import-EditorCommand -Module PowerShellEditorServices.Commands
```

Registers all editor commands in the module PowerShellEditorServices.Commands.

### -------------------------- EXAMPLE 2 --------------------------

```powershell
Get-Command *Editor* | Import-EditorCommand -PassThru
```

Registers all editor commands that contain "Editor" in the name and return all successful imports.

## PARAMETERS

### -Module

Specifies the module to search for exportable editor commands.

```yaml
Type: string[]
Parameter Sets: ByModule
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Command

Specifies the functions to register as editor commands. If the function does not have the PSEditorCommand attribute it will be ignored.

```yaml
Type: string[]
Parameter Sets: ByCommand
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Force

If specified will replace existing editor commands.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru

If specified will return an EditorCommand object for each imported command.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see about_CommonParameters (http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Management.Automation.CommandInfo

You can pass commands to register as editor commands.

## OUTPUTS

### Microsoft.PowerShell.EditorServices.Extensions.EditorCommand

If the "PassThru" parameter is specified editor commands that were successfully registered
will be returned.  This function does not output to the pipeline otherwise.

## NOTES

## RELATED LINKS

