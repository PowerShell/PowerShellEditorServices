---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/main/module/docs/Unregister-EditorCommand.md
schema: 2.0.0
---

# Unregister-EditorCommand

## SYNOPSIS

Unregisters a command which has already been registered in the host editor.

## SYNTAX

```powershell
Unregister-EditorCommand [-Name] <String>
```

## DESCRIPTION

Unregisters a command which has already been registered in the host editor.
An error will be thrown if the specified Name is unknown.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
Unregister-EditorCommand -Name "MyModule.MyFunctionCommand"
```

## PARAMETERS

### -Name

Specifies a unique name which identifies a command which has already been registered.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

## INPUTS

## OUTPUTS

## NOTES

## RELATED LINKS

[Register-EditorCommand](Register-EditorCommand.md)
[Import-EditorCommand](Import-EditorCommand.md)
