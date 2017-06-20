---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/master/module/docs/Out-CurrentFile.md
schema: 2.0.0
---

# Out-CurrentFile

## SYNOPSIS

Sends the output through Out-String to the current open editor file.

## SYNTAX

```powershell
Out-CurrentFile [-InputObject] <Object>
```

## DESCRIPTION

The Out-CurrentFile cmdlet sends output through Out-String to the current open
editor file.

## EXAMPLES

### Example 1

```powershell
Get-Process | Out-CurrentFile
```

Runs the `Get-Process` command and formats its output into the current
editor file.

## PARAMETERS

### -InputObject

The input object to format, either as a parameter or from the pipeline.

```yaml
Type: Object
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

## INPUTS

### System.Object

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
