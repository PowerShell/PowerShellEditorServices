# PowerShell Editor Services

[![Build status](https://ci.appveyor.com/api/projects/status/85tyhckawwxoiim2/branch/master?svg=true)](https://ci.appveyor.com/project/PowerShell/powershelleditorservices/branch/master)

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

## Cloning the Code

To clone the repository and initialize all the submodules at once you can run:

```
git clone --recursive https://github.com/PowerShell/PowerShellEditorServices.git
```

If you have already cloned the repository without `--recursive` option, you can run following commands to initialize the submodules:

```
git submodule init
git submodule update
```

## Contributions Welcome!

We would love to incorporate community contributions into this project.  If you would like to
contribute code, documentation, tests, or bug reports, please read our [Contribution Guide]
(docs/contributing.md) to learn more.
