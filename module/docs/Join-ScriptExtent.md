---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/main/module/docs/Join-ScriptExtent.md
schema: 2.0.0
---

# Join-ScriptExtent

## SYNOPSIS

Combine multiple ScriptExtent objects into a single ScriptExtent.

## SYNTAX

```powershell
Join-ScriptExtent [[-Extent] <IScriptExtent[]>] [<CommonParameters>]
```

## DESCRIPTION

The Join-ScriptExtent function will combine all ScriptExtent objects piped to it into a single extent.  This can be used combine multiple ASTs, tokens, or other script elements into a single object that can then be manipulated or used for more targeted searches.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
$ast = Find-Ast { $_.Arguments.Count -gt 4 } -First
$ast.Arguments[0..1] | Join-ScriptExtent | Set-ScriptExtent -Text ''
```

Finds the first InvokeMemberExpression ast that has over 4 arguments and removes the first two.

## PARAMETERS

### -Extent

Specifies the extents to combine. If a single extent is passed, it will be returned as is. If no extents are passed nothing will be returned. Extents passed from the pipeline are processed after pipeline input completes.

```yaml
Type: IScriptExtent[]
Parameter Sets: (All)
Aliases:

Required: False
Position: 1
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see about_CommonParameters (http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Management.Automation.Language.IScriptExtent

You can pass ScriptExtent objects to this function.  You can also pass objects with a property named "Extent" such as ASTs from Find-Ast or tokens from Get-Token.

## OUTPUTS

### System.Management.Automation.Language.IScriptExtent

The combined ScriptExtent object is returned.

## NOTES

## RELATED LINKS

[ConvertTo-ScriptExtent](ConvertTo-ScriptExtent.md)
[ConvertFrom-ScriptExtent](ConvertFrom-ScriptExtent.md)
[Test-ScriptExtent](Test-ScriptExtent.md)
[Set-ScriptExtent](Set-ScriptExtent.md)
