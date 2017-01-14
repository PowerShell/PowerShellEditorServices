# PowerShell Editor Services

[![Build status](https://ci.appveyor.com/api/projects/status/85tyhckawwxoiim2/branch/master?svg=true)](https://ci.appveyor.com/project/PowerShell/powershelleditorservices/branch/master)

PowerShell Editor Services provides common functionality that is needed
to enable a consistent and robust PowerShell development experience
across multiple editors.

## Features

- The Language Service provides common editor features for the PowerShell language:
  - Code navigation actions (find references, go to definition)
  - Statement completions (IntelliSense)
  - Real-time semantic analysis of scripts using PowerShell Script Analyzer
  - Basic script evaluation
- The Debugging Service simplifies interaction with the PowerShell debugger (breakpoints, variables, call stack, etc)
- The Console Service provides a simplified interactive console interface which implements a rich PSHost implementation:
  - Interactive command execution support, including basic use of native console applications
  - Choice prompt support
  - Input prompt support
  - Get-Credential support (coming soon)
- The Extension Service provides a generalized extensibility model that allows you to
  write new functionality for any host editor that uses PowerShell Editor Services

The core Editor Services library is intended to be consumed in any type of host application, whether
it is a WPF UI, console application, or web service.  A standard console application host is included
so that you can easily consume Editor Services functionality in any editor using the JSON API that it
exposes.

## Documentation

Check out our **[documentation site](http://powershell.github.io/PowerShellEditorServices)** for information about
how to use this project. You can also read our plans for future feature development by looking at the
**[Development Roadmap](https://github.com/PowerShell/PowerShellEditorServices/wiki/Development-Roadmap)**.

## Installation

**TODO**: Add information about acquiring packages from NuGet and npm once those are available.

## Cloning the Code

To clone the repository execute:

```
git clone https://github.com/PowerShell/PowerShellEditorServices.git
```

## Contributions Welcome!

We would love to incorporate community contributions into this project.  If you would like to
contribute code, documentation, tests, or bug reports, please read our [Contribution Guide]
(http://powershell.github.io/PowerShellEditorServices/CONTRIBUTING.html) to learn more.

## Maintainers

- [David Wilson](https://github.com/daviwil) - [@daviwil](http://twitter.com/daviwil)
- [Keith Hill](https://github.com/rkeithhill) - [@r_keith_hill](http://twitter.com/r_keith_hill)

## License

This project is [licensed under the MIT License](LICENSE).
