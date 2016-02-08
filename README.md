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

The core Editor Services library is intended to be consumed in any type of host application, whether
it is a WPF UI, console application, or web service.  A standard console application host is included
so that you can easily consume Editor Services functionality in any editor using the JSON API that it
exposes.

## Documentation

Check out the following two pages for information about how to use the API and host process:

- **[Using the .NET API](docs/using_the_dotnet_api.md)** - Read this if you want to use the API in your .NET application
- **[Using the Host Process](docs/using_the_host_process.md)** - Read this if you want to use the API in a non-.NET application such as a code editor

## Installation

**TODO**: Add information about acquiring packages from NuGet once those are available.

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

## Maintainers

- [David Wilson](https://github.com/daviwil) - [@daviwil](http://twitter.com/daviwil)
- [Keith Hill](https://github.com/rkeithhill) - [@r_keith_hill](http://twitter.com/r_keith_hill)

## License

This project is [licensed under the MIT License](LICENSE.txt).