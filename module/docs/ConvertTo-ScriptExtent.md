---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/main/module/docs/ConvertTo-ScriptExtent.md
schema: 2.0.0
---

# ConvertTo-ScriptExtent

## SYNOPSIS

Converts position and range objects from PowerShellEditorServices to ScriptExtent objects.

## SYNTAX

### ByExtent

```powershell
ConvertTo-ScriptExtent [-Extent <IScriptExtent>] [<CommonParameters>]
```

### ByPosition

```powershell
ConvertTo-ScriptExtent [-StartLineNumber <Int32>] [-StartColumnNumber <Int32>] [-EndLineNumber <Int32>]
 [-EndColumnNumber <Int32>] [-FilePath <String>] [<CommonParameters>]
```

### ByOffset

```powershell
ConvertTo-ScriptExtent [-StartOffsetNumber <Int32>] [-EndOffsetNumber <Int32>] [-FilePath <String>]
 [<CommonParameters>]
```

### ByBuffer

```powershell
ConvertTo-ScriptExtent [-FilePath <String>] [-StartBuffer <BufferPosition>] [-EndBuffer <BufferPosition>]
 [<CommonParameters>]
```

## DESCRIPTION

The ConvertTo-ScriptExtent function can be used to convert any object with position related properties to a ScriptExtent object.  You can also specify the parameters directly to manually create ScriptExtent objects.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
$psEditor.GetEditorContext().SelectedRange | ConvertTo-ScriptExtent
```

Returns a ScriptExtent object of the currently selected range.

### -------------------------- EXAMPLE 2 --------------------------

```powershell
ConvertTo-ScriptExtent -StartOffset 10 -EndOffset 100
```

Returns a ScriptExtent object from a start and end offset.

## PARAMETERS

### -Extent

Specifies a ScriptExtent object to use as a base to create a new editor context aware ScriptExtent object.

```yaml
Type: IScriptExtent
Parameter Sets: ByExtent
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -StartLineNumber

Specifies the starting line number.

```yaml
Type: Int32
Parameter Sets: ByPosition
Aliases: StartLine, Line

Required: False
Position: Named
Default value: 0
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -StartColumnNumber

Specifies the starting column number.

```yaml
Type: Int32
Parameter Sets: ByPosition
Aliases: StartColumn, Column

Required: False
Position: Named
Default value: 0
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -EndLineNumber

Specifies the ending line number.

```yaml
Type: Int32
Parameter Sets: ByPosition
Aliases: EndLine

Required: False
Position: Named
Default value: 0
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -EndColumnNumber

Specifies the ending column number.

```yaml
Type: Int32
Parameter Sets: ByPosition
Aliases: EndColumn

Required: False
Position: Named
Default value: 0
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -StartOffsetNumber

Specifies the starting offset number.

```yaml
Type: Int32
Parameter Sets: ByOffset
Aliases: StartOffset, Offset

Required: False
Position: Named
Default value: 0
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -EndOffsetNumber

Specifies the ending offset number.

```yaml
Type: Int32
Parameter Sets: ByOffset
Aliases: EndOffset

Required: False
Position: Named
Default value: 0
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -FilePath

Specifies the path of the source script file.

```yaml
Type: String
Parameter Sets: ByPosition, ByOffset, ByBuffer
Aliases: File, FileName

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -StartBuffer

Specifies the starting buffer position.

```yaml
Type: BufferPosition
Parameter Sets: ByBuffer
Aliases: Start

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -EndBuffer

Specifies the ending buffer position.

```yaml
Type: BufferPosition
Parameter Sets: ByBuffer
Aliases: End

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see about_CommonParameters (http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Object

You can pass any object with properties that have position related names.  Below is a list of all the property names that can be bound as parameters through the pipeline.

StartLineNumber, StartLine, Line, EndLineNumber, EndLine, StartColumnNumber, StartColumn, Column, EndColumnNumber, EndColumn, StartOffsetNumber, StartOffset, Offset, EndOffsetNumber, EndOffset, StartBuffer, Start, EndBuffer, End

You can also pass IScriptExtent objects to be converted to context aware versions.

## OUTPUTS

### Microsoft.PowerShell.EditorServices.FullScriptExtent

The converted ScriptExtent object will be returned to the pipeline.

## NOTES

## RELATED LINKS

[ConvertFrom-ScriptExtent](ConvertFrom-ScriptExtent.md)
[Test-ScriptExtent](Test-ScriptExtent.md)
[Set-ScriptExtent](Set-ScriptExtent.md)
[Join-ScriptExtent](Join-ScriptExtent.md)
