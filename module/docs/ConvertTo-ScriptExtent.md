---
external help file: PowerShellEditorServices.Commands-help.xml
online version:
schema: 2.0.0
---

# ConvertTo-ScriptExtent

## SYNOPSIS

Converts position and range objects from PowerShellEditorServices to ScriptExtent objects.

## SYNTAX

### ByObject

```powershell
ConvertTo-ScriptExtent [-InputObject <IScriptExtent>] [<CommonParameters>]
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

Converts position and range objects from PowerShellEditorServices to ScriptExtent objects.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
$psEditor.GetEditorContext().SelectedRange | ConvertTo-ScriptExtent
```

Returns a InternalScriptExtent object of the currently selected range.

## PARAMETERS

### -InputObject

This is here so we can pass script extent objects through without any processing.

```yaml
Type: IScriptExtent
Parameter Sets: ByObject
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

You can pass any object with any of the following properties.

StartLineNumber, StartLine, Line
EndLineNumber, EndLine
StartColumnNumber, StartColumn, Column
EndColumnNumber, EndColumn
StartOffsetNumber, StartOffset, Offset
EndOffsetNumber, EndOffset
StartBuffer, Start
EndBuffer, End

Objects of type IScriptExtent will be passed through with no processing.

## OUTPUTS

### System.Management.Automation.Language.IScriptExtent

### System.Management.Automation.Language.InternalScriptExtent

This function will return any IScriptExtent object passed without processing. Objects created
by this function will be of type InternalScriptExtent.

## NOTES

## RELATED LINKS

