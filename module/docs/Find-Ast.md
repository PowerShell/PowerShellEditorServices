---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/master/module/docs/Find-Ast.md
schema: 2.0.0
---

# Find-Ast

## SYNOPSIS

Find a specific element in an abstract syntax tree (AST).

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

The Find-Ast function can be used to easily find a specific AST within a script file. All ASTs following the inital starting ast will be searched, including those that are not part of the same tree.

The behavior of the search (such as direction and criteria) can be changed with parameters.

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
Find-Ast { 'PowerShellVersion' -eq $_ } |
    Find-Ast -First |
    Set-ScriptExtent -Text '4.0' -AsString
```

This example sets the required PowerShell version in a module manifest to 4.0.

First it finds the AST of the PowerShellVersion manifest field, then finds the first AST directly after it and changes the text to '4.0'. This will not work as is if the field is commented.

### -------------------------- EXAMPLE 7 --------------------------

```powershell
Find-Ast { $_.ArgumentName -eq 'ParameterSetName' -and $_.Argument.Value -eq 'ByPosition' } |
    Find-Ast -First -Ancestor { $_ -is [System.Management.Automation.Language.ParameterAst] } |
    ForEach-Object { $_.Name.VariablePath.UserPath }
```

This example gets a list of all parameters that belong to the parameter set 'ByPosition'. First it uses the ArgumentName and Argument properties of NamedAttributeArgumentAst to find the ASTs of arguments to the Parameter attribute that declare the the parameter set 'ByPosition'.  It then finds the closest parent ParameterAst and retrieves the name from it.

### -------------------------- EXAMPLE 8 --------------------------

```powershell
$companyName = Find-Ast {
    $_.Value -eq 'CompanyName' -or
    (Find-Ast -Ast $_ -First -Before).Value -eq 'CompanyName'
}

$previousField = $companyName[0] | Find-Ast -First -Before { $_.StringConstantType -eq 'BareWord' }

$companyNameComments = $companyName.Extent, $previousField.Extent |
    Join-ScriptExtent |
    Get-Token |
    Where-Object Kind -eq 'Comment'

$fullManifestElement = $companyNameComments.Extent, $companyName.Extent | Join-ScriptExtent
```

This example shows off ways you can combine the position functions together to get very specific portions of a script file.  The result of this example is a ScriptExtent that includes a manifest field, value, and all comments above it.

## PARAMETERS

### -FilterScript

Specifies a ScriptBlock that returns $true if an AST should be returned. Uses $PSItem and $_ like Where-Object. If not specified all ASTs will be returned.

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

Specifies the starting AST. The default is the AST of the current file in PowerShell Editor Services.

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

If specified only children of the starting AST will be searched. If specified with the "Before" parameter then only ancestors will be searched.

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

If specified will return only the first result. This will be the closest AST that matches.

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

If specified will return only the last result. This will be the furthest AST that matches.

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

If specified will only search ancestors of the starting AST.  This is a convenience parameter that acts the same as the "Family" and "Before" parameters when used together.

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

If specified the starting AST will be included if matched.

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

If specified, this function will return the smallest AST that the cursor is within.

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

You can pass ASTs to search to this function.

## OUTPUTS

### System.Management.Automation.Language.Ast

ASTs that match the criteria will be returned to the pipeline.

## NOTES

## RELATED LINKS

[Get-Token](Get-Token.md)
[Set-ScriptExtent](Set-ScriptExtent.md)
[ConvertTo-ScriptExtent](ConvertTo-ScriptExtent.md)
