---
external help file: Microsoft.PowerShell.EditorServices.VSCode.dll-Help.xml
Module Name: PowerShellEditorServices.VSCode
online version:
schema: 2.0.0
---

# Write-VSCodeHtmlContentView

## SYNOPSIS

Writes an HTML fragment to an HtmlContentView.

## SYNTAX

```
Write-VSCodeHtmlContentView [-HtmlContentView] <IHtmlContentView> [-AppendedHtmlBodyContent] <String>
 [<CommonParameters>]
```

## DESCRIPTION

Writes an HTML fragment to an HtmlContentView.  This new fragment is appended to the existing content, useful in cases where the output will be appended to an ongoing output stream.

## EXAMPLES

### Example 1

```powershell
Write-VSCodeHtmlContentView -HtmlContentView $htmlContentView -AppendedHtmlBodyContent "<h3>Appended content</h3>"
```

### Example 2

```powershell
Write-VSCodeHtmlContentView -View $htmlContentView -Content "<h3>Appended content</h3>"
```

## PARAMETERS

### -AppendedHtmlBodyContent

The HTML content that will be appended to the view's `<body>` element content.

```yaml
Type: String
Parameter Sets: (All)
Aliases: Content

Required: True
Position: 1
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -HtmlContentView

The HtmlContentView where content will be appended.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
