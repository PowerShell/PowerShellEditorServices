---
external help file: Microsoft.PowerShell.EditorServices.VSCode.dll-Help.xml
Module Name: PowerShellEditorServices.VSCode
online version:
schema: 2.0.0
---

# Set-VSCodeHtmlContentView

## SYNOPSIS

Sets the content of an HtmlContentView.

## SYNTAX

```
Set-VSCodeHtmlContentView [-HtmlContentView] <IHtmlContentView> [-HtmlBodyContent] <String>
 [[-JavaScriptPaths] <String[]>] [[-StyleSheetPaths] <String[]>] [<CommonParameters>]
```

## DESCRIPTION

Sets the content of an HtmlContentView. If an empty string is passed, it causes the view's content to be cleared.

## EXAMPLES

### Example 1

```powershell
Set-VSCodeHtmlContentView -HtmlContentView $htmlContentView -HtmlBodyContent "<h1>Hello world!</h1>"
```

Set the view content with an h1 header.

### Example 2

```powershell
Set-VSCodeHtmlContentView -View $htmlContentView -Content ""
```

Clear the view.

## PARAMETERS

### -HtmlBodyContent

The HTML content that will be placed inside the `<body>` tag of the view.

```yaml
Type: String
Parameter Sets: (All)
Aliases: Content

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HtmlContentView

The HtmlContentView where content will be set.

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

### -JavaScriptPaths

An array of paths to JavaScript files that will be loaded into the view.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StyleSheetPaths

An array of paths to stylesheet (CSS) files that will be loaded into the view.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 3
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
