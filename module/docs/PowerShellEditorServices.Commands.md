---
Module Name: PowerShellEditorServices.Commands
Module Guid: 6064d846-0fa0-4b6d-afc1-11e5bed3c4a9
Download Help Link:
Help Version:
Locale: en-US
---

# PowerShellEditorServices.Commands Module

## Description

Module to facilitate easy manipulation of script files and editor features.

## PowerShellEditorServices.Commands Cmdlets

### [ConvertFrom-ScriptExtent](ConvertFrom-ScriptExtent.md)

The ConvertFrom-ScriptExtent function converts ScriptExtent objects to types used in methods found in the $psEditor API.

### [ConvertTo-ScriptExtent](ConvertTo-ScriptExtent.md)

The ConvertTo-ScriptExtent function can be used to convert any object with position-related properties to a ScriptExtent object.  You can also specify the parameters directly to manually create ScriptExtent objects.

### [Find-Ast](Find-Ast.md)

The Find-Ast function can be used to easily find a specific AST within a script file. All ASTs following the initial starting AST will be searched, including those that are not part of the same tree.

### [Get-Token](Get-Token.md)

The Get-Token function can retrieve tokens from the current editor context, or from a ScriptExtent object. You can then use the ScriptExtent functions to manipulate the text at its location.

### [Import-EditorCommand](Import-EditorCommand.md)

The Import-EditorCommand function will search the specified module for functions tagged as editor commands and register them with PowerShell Editor Services. By default, if a module is specified only exported functions will be processed.

Alternatively, you can specify command info objects (like those from the Get-Command cmdlet) to be processed directly.

### [Join-ScriptExtent](Join-ScriptExtent.md)

The Join-ScriptExtent function will combine all ScriptExtent objects piped to it into a single extent.  This can be used to combine multiple ASTs, tokens, or other script elements into a single object that can then be manipulated or used for more targeted searches.

### [Set-ScriptExtent](Set-ScriptExtent.md)

The Set-ScriptExtent function can insert or replace text at a specified position in a file open in PowerShell Editor Services.

You can use the Find-Ast function to easily find the desired extent.

### [Test-ScriptExtent](Test-ScriptExtent.md)

The Test-ScriptExtent function can be used to determine if a ScriptExtent object is before, after, or inside another ScriptExtent object.  You can also test for any combination of these with separate ScriptExtent objects to test against.
