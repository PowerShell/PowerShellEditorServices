---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/main/module/docs/Set-ScriptExtent.md
schema: 2.0.0
---

# Set-ScriptExtent

## SYNOPSIS

Replaces text at a specified IScriptExtent object.

## SYNTAX

### __AllParameterSets (Default)

```powershell
Set-ScriptExtent [-Text] <PSObject> [-Extent <IScriptExtent>] [<CommonParameters>]
```

### AsString

```powershell
Set-ScriptExtent [-Text] <PSObject> [-AsString] [-Extent <IScriptExtent>] [<CommonParameters>]
```

### AsArray

```powershell
Set-ScriptExtent [-Text] <PSObject> [-AsArray] [-Extent <IScriptExtent>] [<CommonParameters>]
```

## DESCRIPTION

The Set-ScriptExtent function can insert or replace text at a specified position in a file open in PowerShell Editor Services.

You can use the Find-Ast function to easily find the desired extent.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
Find-Ast { 'gci' -eq $_ } | Set-ScriptExtent -Text 'Get-ChildItem'
```

Replaces all instances of 'gci' with 'Get-ChildItem'

### -------------------------- EXAMPLE 2 --------------------------

```powershell
$manifestAst = Find-Ast { 'FunctionsToExport' -eq $_ } | Find-Ast -First
$manifestAst | Set-ScriptExtent -Text (gci .\src\Public).BaseName -AsArray
```

Replaces the current value of FunctionsToExport in a module manifest with a list of files in the Public folder as a string array literal expression.

## PARAMETERS

### -Text

Specifies the text to insert in place of the extent.  Any object can be specified, but will be converted to a string before being passed to PowerShell Editor Services.

```yaml
Type: PSObject
Parameter Sets: (All)
Aliases: Value

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AsString

Specifies to insert as a single quoted string expression.

```yaml
Type: SwitchParameter
Parameter Sets: AsString
Aliases:

Required: True
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -AsArray

Specifies to insert as a single quoted string list.  The list is separated by comma and new line, and will be adjusted to a hanging indent.

```yaml
Type: SwitchParameter
Parameter Sets: AsArray
Aliases:

Required: True
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Extent

Specifies the extent to replace within the editor.

```yaml
Type: IScriptExtent
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: (Find-Ast -AtCursor).Extent
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see about_CommonParameters (http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.Management.Automation.Language.IScriptExtent

You can pass ScriptExtent objects to this function.  You can also pass objects with a property named "Extent" such as ASTs from Find-Ast or tokens from Get-Token.

## OUTPUTS

### None

## NOTES

## RELATED LINKS

[Find-Ast](Find-Ast.md)
[ConvertTo-ScriptExtent](ConvertTo-ScriptExtent.md)
[ConvertFrom-ScriptExtent](ConvertFrom-ScriptExtent.md)
[Test-ScriptExtent](Test-ScriptExtent.md)
[Join-ScriptExtent](Join-ScriptExtent.md)
