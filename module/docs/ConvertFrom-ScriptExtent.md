---
external help file: PowerShellEditorServices.Commands-help.xml
online version:
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

Translates IScriptExtent object properties into constructors for some common PowerShell EditorServices types.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
$sb = { Get-ChildItem 'Documents' }
$sb.Ast | Find-Ast { $_ -eq 'Documents' } | ConvertFrom-ScriptExtent -BufferRange
```

Gets the buffer range of the string expression "Documents".

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

You can pipe IScriptExtent objects to be converted.

## OUTPUTS

### Microsoft.PowerShell.EditorServices.BufferRange

### Microsoft.PowerShell.EditorServices.BufferPosition

This function will return an extent converted to one of the above types depending on switch
choices.

## NOTES

## RELATED LINKS

