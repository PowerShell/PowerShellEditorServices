---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/master/module/docs/Register-EditorCommand.md
schema: 2.0.0
---

# Register-EditorCommand

## SYNOPSIS

Registers a command which can be executed in the host editor.

## SYNTAX

### Function

```powershell
Register-EditorCommand -Name <String> -DisplayName <String> -Function <String> [-SuppressOutput]
```

### ScriptBlock

```powershell
Register-EditorCommand -Name <String> -DisplayName <String> -ScriptBlock <ScriptBlock> [-SuppressOutput]
```

## DESCRIPTION

Registers a command which can be executed in the host editor. This
command will be shown to the user either in a menu or command palette.
Upon invoking this command, either a function/cmdlet or ScriptBlock will
be executed depending on whether the -Function or -ScriptBlock parameter
was used when the command was registered.

This command can be run multiple times for the same command so that its
details can be updated. However, re-registration of commands should only
be used for development purposes, not for dynamic behavior.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
Register-EditorCommand -Name "MyModule.MyFunctionCommand" -DisplayName "My function command" -Function Invoke-MyCommand -SuppressOutput
```

### -------------------------- EXAMPLE 2 --------------------------

```powershell
Register-EditorCommand -Name "MyModule.MyScriptBlockCommand" -DisplayName "My ScriptBlock command" -ScriptBlock { Write-Output "Hello from my command!" }
```

## PARAMETERS

### -Name

Specifies a unique name which can be used to identify this command.
This name is not displayed to the user.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DisplayName

Specifies a display name which is displayed to the user.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Function

Specifies a function or cmdlet name which will be executed when the user
invokes this command. This function may take a parameter called $context
which will be populated with an EditorContext object containing information
about the host editor's state at the time the command was executed.

```yaml
Type: String
Parameter Sets: Function
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ScriptBlock

Specifies a ScriptBlock which will be executed when the user invokes this
command. This ScriptBlock may take a parameter called $context
which will be populated with an EditorContext object containing information
about the host editor's state at the time the command was executed.

```yaml
Type: ScriptBlock
Parameter Sets: ScriptBlock
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SuppressOutput

If provided, causes the output of the editor command to be suppressed when
it is run. Errors that occur while running this command will still be
written to the host.

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

## INPUTS

## OUTPUTS

## NOTES

## RELATED LINKS

[Unregister-EditorCommand](Unregister-EditorCommand.md)
[Import-EditorCommand](Import-EditorCommand.md)
