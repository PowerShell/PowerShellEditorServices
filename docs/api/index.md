# API Reference

The .NET API for PowerShell Editor Services is organized in a way that allows
you to easily get started using all of its services but also giving you the
option to only use the services you care about in your application.

The best starting point is the @Microsoft.PowerShell.EditorServices.EditorSession
class which can start up all of the following services for use in a single editing
session.

Use the @Microsoft.PowerShell.EditorServices.LanguageService to provide language
intelligence behaviors like finding the references or definition of a cmdlet or variable.

Use the @Microsoft.PowerShell.EditorServices.AnalysisService to provide rule-based
analysis of scripts using [PowerShell Script Analyzer](https://github.com/PowerShell/PSScriptAnalyzer).

Use the @Microsoft.PowerShell.EditorServices.DebugService to easily interact with
the PowerShell debugger.

Use the @Microsoft.PowerShell.EditorServices.Console.ConsoleService to provide interactive
console support in the user's editor.

Use the @Microsoft.PowerShell.EditorServices.Services.ExtensionService to allow
the user to extend the host editor with new capabilities using PowerShell code.

The core of all the services is the @Microsoft.PowerShell.EditorServices.PowerShellContext
class.  This class manages a session's runspace and handles script and command
execution no matter what state the runspace is in.
