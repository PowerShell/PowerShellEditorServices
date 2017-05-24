# PowerShell Editor Services

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

## Build status of master branches

| AppVeyor (Windows)                                                                                                                                                                        | Travis CI (Linux / macOS)                                                                                                                                 |
|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| [![Build status](https://ci.appveyor.com/api/projects/status/85tyhckawwxoiim2/branch/master?svg=true)](https://ci.appveyor.com/project/PowerShell/powershelleditorservices/branch/master) | [![Build Status](https://travis-ci.org/Powershell/PowerShellEditorServices.svg?branch=master)](https://travis-ci.org/powershell/PowerShellEditorServices) |

## Documentation

Check out our **[documentation site](http://powershell.github.io/PowerShellEditorServices)** for information about
how to use this project. You can also read our plans for future feature development by looking at the
**[Development Roadmap](https://github.com/PowerShell/PowerShellEditorServices/wiki/Development-Roadmap)**.

## Installation

**TODO**: Add information about acquiring packages from NuGet and npm once those are available.

## Development


### 1. Install PowerShell if necessary

If you are using Windows, skip this step.  If you are using Linux or macOS, you will need to
install PowerShell by following [these instructions](https://github.com/PowerShell/PowerShell#get-powershell).

If you are using macOS you will need to download the latest version of OpenSSL. The easiest way to get this is from [Homebrew](http://brew.sh/). After installing Homebrew execute the following commands:

```
  brew update
  brew install openssl
  mkdir -p /usr/local/lib
  ln -s /usr/local/opt/openssl/lib/libcrypto.1.0.0.dylib /usr/local/lib/
  ln -s /usr/local/opt/openssl/lib/libssl.1.0.0.dylib /usr/local/lib/
```
### 2. Clone the GitHub repository:

```
git clone https://github.com/PowerShell/PowerShellEditorServices.git
```

### 3. Install [Invoke-Build](https://github.com/nightroman/Invoke-Build)

This step requires PowerShellGet, included by default with PowerShell v5 and up
but installable on [PowerShell v3 and v4](https://github.com/PowerShell/PowerShellGet#get-powershellget-module-for-powershell-versions-30-and-40).

```powershell
Install-Module InvokeBuild -Scope CurrentUser
```

Now you're ready to build the code.  You can do so in one of two ways:

### Building the code from PowerShell

```powershell
PS C:\path\to\PowerShellEditorServices> Invoke-Build Build
```

### Building the code from Visual Studio Code

Open the PowerShellEditorServices folder that you cloned locally and press <kbd>Ctrl+Shift+B</kbd>
(or <kbd>Cmd+Shift+B</kbd> on macOS).

## Contributions Welcome!

We would love to incorporate community contributions into this project.  If you would like to
contribute code, documentation, tests, or bug reports, please read our [Contribution Guide]
(http://powershell.github.io/PowerShellEditorServices/CONTRIBUTING.html) to learn more.

## Maintainers

- [David Wilson](https://github.com/daviwil) - [@daviwil](http://twitter.com/daviwil)
- [Keith Hill](https://github.com/rkeithhill) - [@r_keith_hill](http://twitter.com/r_keith_hill)

## License

This project is [licensed under the MIT License](LICENSE).
