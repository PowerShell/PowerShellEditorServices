---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/main/module/docs/ConvertFrom-ScriptExtent.md
schema: 2.0.0
---

# ConvertFrom-ScriptExtent

## SYNOPSIS

Converts IScriptExtent objects to some common EditorServices types.

## SYNTAX

### BufferRange

```powershell
ConvertFrom-ScriptExtent -Extent <IScriptExtent[]> [-BufferRange] [<CommonParameters>]
```

### BufferPosition

```powershell
ConvertFrom-ScriptExtent -Extent <IScriptExtent[]> [-BufferPosition] [-Start] [-End] [<CommonParameters>]
```

## DESCRIPTION

The ConvertFrom-ScriptExtent function converts ScriptExtent objects to types used in methods found in the $psEditor API.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
$range = Find-Ast -First { [System.Management.Automation.Language.CommandAst] } |
    ConvertFrom-ScriptExtent -BufferRange

$psEditor.GetEditorContext().SetSelection($range)
```

Convert the extent of the first CommandAst to a BufferRange and use that to select it with the $psEditor API.

## PARAMETERS

### -Extent

Specifies the extent to be converted.

```yaml
Type: IScriptExtent[]
Parameter Sets: (All)
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -BufferRange

If specified will convert extents to BufferRange objects.

```yaml
Type: SwitchParameter
Parameter Sets: BufferRange
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -BufferPosition

If specified will convert extents to BufferPosition objects.

```yaml
Type: SwitchParameter
Parameter Sets: BufferPosition
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Start

Specifies to use the start of the extent when converting to types with no range. This is the default.

```yaml
Type: SwitchParameter
Parameter Sets: BufferPosition
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -End

Specifies to use the end of the extent when converting to types with no range.

```yaml
Type: SwitchParameter
Parameter Sets: BufferPosition
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

### System.Management.Automation.Language.IScriptExtent

You can pass ScriptExtent objects to this function.  You can also pass objects with a property named "Extent" such as ASTs from Find-Ast or tokens from Get-Token.

## OUTPUTS

### Microsoft.PowerShell.EditorServices.BufferRange

### Microsoft.PowerShell.EditorServices.BufferPosition

This function will return the converted object of one of the above types depending on parameter switch choices.

## NOTES

## RELATED LINKS

[ConvertTo-ScriptExtent](ConvertTo-ScriptExtent.md)
[Test-ScriptExtent](Test-ScriptExtent.md)
[Set-ScriptExtent](Set-ScriptExtent.md)
[Join-ScriptExtent](Join-ScriptExtent.md)
