---
external help file: Microsoft.PowerShell.EditorServices.VSCode.dll-Help.xml
Module Name: PowerShellEditorServices.VSCode
online version:
schema: 2.0.0
---

# Show-VSCodeHtmlContentView

## SYNOPSIS

Shows an HtmlContentView.

## SYNTAX

```
Show-VSCodeHtmlContentView [-HtmlContentView] <IHtmlContentView> [[-ViewColumn] <ViewColumn>]
 [<CommonParameters>]
```

## DESCRIPTION

Shows an HtmlContentView that has been created and not shown yet or has previously been closed.

## EXAMPLES

### Example 1

```powershell
Show-VSCodeHtmlContentView -HtmlContentView $htmlContentView
```

Shows the view in the first editor column.

### Example 2

```powershell
Show-VSCodeHtmlContentView -View $htmlContentView -Column Three
```

Shows the view in the third editor column.

## PARAMETERS

### -HtmlContentView

The HtmlContentView that will be shown.

```yaml
Type: IHtmlContentView
Parameter Sets: (All)
Aliases: View

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ViewColumn

If specified, causes the new view to be displayed in the specified column.

```yaml
Type: ViewColumn
Parameter Sets: (All)
Aliases: Column
Accepted values: One, Two, Three

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
