---
external help file: Microsoft.PowerShell.EditorServices.VSCode.dll-Help.xml
Module Name: PowerShellEditorServices.VSCode
online version:
schema: 2.0.0
---

# New-VSCodeHtmlContentView

## SYNOPSIS

Creates a custom view in Visual Studio Code which displays HTML content.

## SYNTAX

```
New-VSCodeHtmlContentView [-Title] <String> [[-ShowInColumn] <ViewColumn>] [<CommonParameters>]
```

## DESCRIPTION

Creates a custom view in Visual Studio Code which displays HTML content.

## EXAMPLES

### Example 1

```powershell
$htmlContentView = New-VSCodeHtmlContentView -Title "My Custom View"
```

Create a new view called "My Custom View".

### Example 2

```powershell
$htmlContentView = New-VSCodeHtmlContentView -Title "My Custom View" -ShowInColumn Two
```

Create a new view and show it in the second view column.

## PARAMETERS

### -ShowInColumn

If specified, causes the new view to be displayed in the specified column.
If unspecified, the Show-VSCodeHtmlContentView cmdlet will need to be used to display the view.

```yaml
Type: ViewColumn
Parameter Sets: (All)
Aliases:
Accepted values: One, Two, Three

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Title

The title of the view.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### Microsoft.PowerShell.EditorServices.VSCode.CustomViews.IHtmlContentView

## NOTES

## RELATED LINKS
