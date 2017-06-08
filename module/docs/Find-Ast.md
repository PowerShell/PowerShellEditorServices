---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/master/module/docs/Find-Ast.md
schema: 2.0.0
---

# Find-Ast

## SYNOPSIS

Search for a ast within an ast.

## SYNTAX

### FilterScript (Default)

```powershell
Find-Ast [[-FilterScript] <ScriptBlock>] [-Ast <Ast>] [-Before] [-Family] [-First] [-Last] [-Ancestor]
 [-IncludeStartingAst] [<CommonParameters>]
```

### AtCursor

```powershell
Find-Ast [-AtCursor] [<CommonParameters>]
```

## DESCRIPTION

The Find-Ast function can be used to easily find a specific ast from a starting ast. By default children asts will be searched, but ancestor asts can also be searched by specifying the "Ancestor" switch parameter.

Additionally, you can find the Ast closest to the cursor with the "AtCursor" switch parameter.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
Find-Ast
```

Returns all asts in the currently open file in the editor.

### -------------------------- EXAMPLE 2 --------------------------

```powershell
Find-Ast -First -IncludeStartingAst
```

Returns the top level ast in the currently open file in the editor.

### -------------------------- EXAMPLE 3 --------------------------

```powershell
Find-Ast { $PSItem -is [FunctionDefinitionAst] }
```

Returns all function definition asts in the ast of file currently open in the editor.

### -------------------------- EXAMPLE 4 --------------------------

```powershell
Find-Ast { $_.Member }
```

Returns all member expressions in the file currently open in the editor.

### -------------------------- EXAMPLE 5 --------------------------

```powershell
Find-Ast { $_.InvocationOperator -eq 'Dot' } | Find-Ast -Family { $_.VariablePath }
```

Returns all variable expressions used in a dot source expression.

### -------------------------- EXAMPLE 6 --------------------------

```powershell
Find-Ast { 'PowerShellVersion' -eq $_ } | Find-Ast -First | Set-ScriptExtent -Text "'4.0'"
```

First finds the ast of the PowerShellVersion manifest tag, then finds the first ast after it and changes the text to '4.0'. This will not work as is if the field is commented.

## PARAMETERS

### -FilterScript

Specifies a ScriptBlock that returns $true if an ast should be returned. Uses $PSItem and $_ like Where-Object. If not specified all asts will be returned.

```yaml
Type: ScriptBlock
Parameter Sets: FilterScript
Aliases:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Ast

Specifies the starting ast. The default is the ast of the current file in PowerShell Editor Services.

```yaml
Type: Ast
Parameter Sets: FilterScript
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -Before

If specified the direction of the search will be reversed.

```yaml
Type: SwitchParameter
Parameter Sets: FilterScript
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Family

If specified only children of the starting ast will be searched. If specified with the "Before" parameter then only ancestors will be searched.

```yaml
Type: SwitchParameter
Parameter Sets: FilterScript
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -First

If specified will return only the first result. This will be the closest ast that matches.

```yaml
Type: SwitchParameter
Parameter Sets: FilterScript
Aliases: Closest, F

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Last

If specified will return only the last result. This will be the furthest ast that matches.

```yaml
Type: SwitchParameter
Parameter Sets: FilterScript
Aliases: Furthest

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Ancestor

If specified will only search ancestors of the starting ast.  This is a convenience parameter that acts the same as the "Family" and "Before" parameters when used together.

```yaml
Type: SwitchParameter
Parameter Sets: FilterScript
Aliases: Parent

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeStartingAst

If specified the starting ast will be included if matched.

```yaml
Type: SwitchParameter
Parameter Sets: FilterScript
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -AtCursor

If specified, this function will return the smallest ast that the cursor is within. Requires PowerShell Editor Services.

```yaml
Type: SwitchParameter
Parameter Sets: AtCursor
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

### System.Management.Automation.Language.Ast

You can pass asts to search to this function.

## OUTPUTS

### System.Management.Automation.Language.Ast

Asts that match the criteria will be returned to the pipeline.

## NOTES

## RELATED LINKS

