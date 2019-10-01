---
external help file: Microsoft.PowerShell.EditorServices.VSCode.dll-Help.xml
Module Name: PowerShellEditorServices.VSCode
online version:
schema: 2.0.0
---

# Close-VSCodeHtmlContentView

## SYNOPSIS

Closes an HtmlContentView.

## SYNTAX

```
Close-VSCodeHtmlContentView [-HtmlContentView] <IHtmlContentView> [<CommonParameters>]
```

## DESCRIPTION

Closes an HtmlContentView inside of Visual Studio Code if it is displayed.

## EXAMPLES

### Example 1

```powershell
Close-VSCodeHtmlContentView -HtmlContentView $view
```

## PARAMETERS

### -HtmlContentView

The HtmlContentView to be closed.

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

### None

## OUTPUTS

### System.Object

## NOTES

## RELATED LINKS
