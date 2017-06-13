---
external help file: PowerShellEditorServices.Commands-help.xml
online version: https://github.com/PowerShell/PowerShellEditorServices/tree/master/module/docs/Test-ScriptExtent.md
schema: 2.0.0
---

# Test-ScriptExtent

## SYNOPSIS

Test the position of a ScriptExtent object in relation to another.

## SYNTAX

```powershell
Test-ScriptExtent [[-Extent] <IScriptExtent>] [-Inside <IScriptExtent>] [-After <IScriptExtent>]
 [-Before <IScriptExtent>] [-PassThru]
```

## DESCRIPTION

The Test-ScriptExtent function can be used to determine if a ScriptExtent object is before, after, or inside another ScriptExtent object.  You can also test for any combination of these with separate ScriptExtent objects to test against.

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
Test-ScriptExtent -Extent $extent1 -Inside $extent2
```

Test if $extent1 is inside $extent2.

### -------------------------- EXAMPLE 2 --------------------------

```powershell
$extentList | Test-ScriptExtent -Before $extent1 -After $extent2 -PassThru
```

Return all extents in $extentList that are before $extent1 but after $extent2.

## PARAMETERS

### -Extent

Specifies the extent to test against.

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

### -Inside

Specifies that the reference extent must be inside this extent for the test to pass.

```yaml
Type: IScriptExtent
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -After

Specifies that the reference extent must be after this extent for the test to pass.

```yaml
Type: IScriptExtent
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Before

Specifies that the reference extent must be before this extent for the test to pass.

```yaml
Type: IScriptExtent
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru

If specified this function will return the reference extent if the test passed instead of returning a boolean.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

## INPUTS

### System.Management.Automation.Language.IScriptExtent

You can pass ScriptExtent objects to this function.  You can also pass objects with a property named "Extent" such as ASTs from Find-Ast or tokens from Get-Token.

## OUTPUTS

### Boolean, System.Management.Automation.Language.IScriptExtent

The result of the test will be returned to the pipeline.

If the "PassThru" parameter is specified and the test passed, the reference script extent will be returned instead.

## NOTES

## RELATED LINKS

[ConvertTo-ScriptExtent](ConvertTo-ScriptExtent.md)
[ConvertFrom-ScriptExtent](ConvertFrom-ScriptExtent.md)
[Set-ScriptExtent](Set-ScriptExtent.md)
[Join-ScriptExtent](Join-ScriptExtent.md)
