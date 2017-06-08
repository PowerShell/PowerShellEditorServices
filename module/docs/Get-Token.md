---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/master/module/docs/Get-Token.md
schema: 2.0.0
---

# Get-Token

## SYNOPSIS

Get parser tokens from a script position.

## SYNTAX

```powershell
Get-Token [[-Extent] <IScriptExtent>]
```

## DESCRIPTION

The Get-Token function can retrieve tokens from the current editor context, or from a ScriptExtent object. You can then use the ScriptExtent functions to manipulate the text at it's location.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
using namespace System.Management.Automation.Language
Find-Ast { $_ -is [IfStatementAst] } -First | Get-Token
```

Gets all tokens from the first IfStatementAst.

### -------------------------- EXAMPLE 2 --------------------------

```powershell
Get-Token | Where-Object { $_.Kind -eq 'Comment' }
```

Gets all comment tokens.

## PARAMETERS

### -Extent

Specifies the extent that a token must be within to be returned.

```yaml
Type: IScriptExtent
Parameter Sets: (All)
Aliases:

Required: False
Position: 1
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

## INPUTS

### System.Management.Automation.Language.IScriptExtent

You can pass extents to get tokens from to this function. You can also pass objects that with a property named "Extent", like Ast objects from the Find-Ast function.

## OUTPUTS

### System.Management.Automation.Language.Token

## NOTES

## RELATED LINKS
