# PowerShell Editor Services

[![Join the chat at https://gitter.im/PowerShell/PowerShellEditorServices](https://badges.gitter.im/PowerShell/PowerShellEditorServices.svg)](https://gitter.im/PowerShell/PowerShellEditorServices?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

PowerShell Editor Services is a PowerShell module that provides common
functionality needed to enable a consistent and robust PowerShell development
experience in almost any editor or integrated development environment (IDE).

## Features

- The Language Service provides common editor features for the PowerShell language:
  - Code navigation actions (find references, go to definition)
  - Statement completions (IntelliSense)
  - Real-time semantic analysis of scripts using PowerShell Script Analyzer
- The Debugging Service simplifies interaction with the PowerShell debugger (breakpoints, variables, call stack, etc)
- The [$psEditor API](http://powershell.github.io/PowerShellEditorServices/guide/extensions.html) enables scripting of the host editor
- A full, terminal-based Integrated Console experience for interactive development and debugging

### Important note regarding the .NET APIs in Microsoft.PowerShell.EditorServices.dll

With the 1.0 release of PowerShell Editor Services, we have deprecated the public APIs
of the following classes:

- EditorSession
- LanguageService
- DebugService
- ConsoleService
- AnalysisService
- ExtensionService
- TemplateService

The intended usage model is now to host PowerShell Editor Services within powershell.exe
and communicate with it over TCP sockets via the [Language Server Protocol](https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md)
and [Debug Adapter Protocol](https://github.com/Microsoft/vscode-debugadapter-node/blob/master/protocol/src/debugProtocol.ts).
Detailed usage documentation for this module is coming soon!

## Build Status

| AppVeyor (Windows)                                                                                                                                                                        | Travis CI (Linux / macOS)                                                                                                                                 |
|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| [![Build status](https://ci.appveyor.com/api/projects/status/85tyhckawwxoiim2/branch/master?svg=true)](https://ci.appveyor.com/project/PowerShell/powershelleditorservices/branch/master) | [![Build Status](https://travis-ci.org/PowerShell/PowerShellEditorServices.svg?branch=master)](https://travis-ci.org/PowerShell/PowerShellEditorServices) |

## Documentation

Check out our **[documentation site](http://powershell.github.io/PowerShellEditorServices)** for information about
how to use this project. You can also read our plans for future feature development by looking at the
**[Development Roadmap](https://github.com/PowerShell/PowerShellEditorServices/wiki/Development-Roadmap)**.

## Installation

**TODO**: Add information about acquiring packages from NuGet and npm once those are available.

## Development

### 1. On Linux or macOS, install PowerShell Core

If you are using Windows, skip this step.  If you are using Linux or macOS, you will need to
install PowerShell by following [these instructions](https://github.com/PowerShell/PowerShell#get-powershell).

If you are using macOS you will need to download the latest version of OpenSSL. The easiest way to get this is from
[Homebrew](http://brew.sh/). After installing Homebrew execute the following commands:

```
  brew update
  brew install openssl
  mkdir -p /usr/local/lib
  ln -s /usr/local/opt/openssl/lib/libcrypto.1.0.0.dylib /usr/local/lib/
  ln -s /usr/local/opt/openssl/lib/libssl.1.0.0.dylib /usr/local/lib/
```
### 2. On Windows, install the .NET 4.5.1 Targeting Pack

**NOTE: This is only necessary if you don't have Visual Studio installed**

If you try to build the code and receive an error about a missing .NET 4.5.1
Targeting Pack, you should download and install the [.NET Framework 4.5.2 Developer Pack](https://www.microsoft.com/en-us/download/details.aspx?id=42637).

### 3. Clone the GitHub repository:

```
git clone https://github.com/PowerShell/PowerShellEditorServices.git
```

### 4. Install [Invoke-Build](https://github.com/nightroman/Invoke-Build)

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
- [Kapil Borle](https://github.com/kapilmb) - [@kmborle](http://twitter.com/kmborle)
- [Patrick Meinecke](https://github.com/SeeminglyScience) - [@SeeminglyScienc](http://twitter.com/SeeminglyScienc)

## License

This project is [licensed under the MIT License](LICENSE).
