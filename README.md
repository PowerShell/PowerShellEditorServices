# PowerShell Editor Services

PowerShell Editor Services provides common functionality that is needed 
to enable a consistent and robust PowerShell development experience 
across multiple editors.

## Features

- The Language Service provides code navigation actions (find references, go to definition) and statement completions (IntelliSense)
- The Analysis Service integrates PowerShell Script Analyzer to provide real-time semantic analysis of scripts
- The Console Service provides a simplified PowerShell host for an interactive console (REPL)
- The Debugging Service simplifies interaction with the PowerShell debugger (breakpoints, locals, etc) - COMING SOON

The core Editor Services library is intended to be consumed in any type of host application, whether
it is a WPF UI, console application, or web service.  A standard console application host is included
so that you can easily consume Editor Services functionality in any editor using either the included
standard input/output transport protocol or a transport of your own design.



