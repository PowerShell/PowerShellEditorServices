# PowerShell Editor Services Release History

## 0.5.0
### Thursday, March 10, 2016

### Support for PowerShell v3 and v4

- Support for PowerShell v3 and v4 is now complete!  Note that for this release,
  Script Analyzer support has been disabled for PS v3 and v4 until we implement
  a better strategy for integrating it as a module dependency

### Debugging improvements

- Added support for command breakpoints
- Added support for conditional breakpoints
- Improved the debug adapter startup sequence to handle new VS Code debugging features

### Other improvements

- `using 'module'` now resolves relative paths correctly, removing a syntax error that
  previously appeared when relative paths were used
- Calling `Read-Host -AsSecureString` or `Get-Credential` from the console now shows an
  appropriate "not supported" error message instead of crashing the language service. 
  Support for these commands will be added in a later release.

## 0.4.3
### Monday, February 29, 2016

- Fixed #166: PowerShell Editor Services should be usable without PSScriptAnalyzer binaries

## 0.4.2
### Wednesday, February 17, 2016

- Fixed #127: Update to PSScriptAnalyzer 1.4.0
- Fixed #149: Scripts fail to launch in the debugger if working directory path contains spaces
- Fixed #153: Script Analyzer integration is not working in 0.4.1 release
- Fixed #159: LanguageServer.Shutdown method hangs while waiting for remaining buffered output to flush

## 0.4.1
### Tuesday, February 9, 2016

- Fixed #147: Running native console apps causes their stdout to be written in the Host

## 0.4.0
### Monday, February 8, 2016

#### Debugging improvements

- A new `Microsoft.PowerShell.EditorServices.Host.x86.exe` executable has been added to enable 32-bit PowerShell sessions on 64-bit machines (thanks [@adamdriscoll](https://github.com/adamdriscoll)!)
- You can now pass arguments to scripts in the debugger with the `LaunchRequest.Args` parameter (thanks [@rkeithhill](https://github.com/rkeithhill)!)
- You can also set the working directory where the script is run by setting the `LaunchRequest.Cwd` parameter to an absolute path (thanks [@rkeithhill](https://github.com/rkeithhill)!)

#### Console improvements

- Improved PowerShell console output formatting and performance
  - The console prompt is now displayed after a command is executed
  - Command execution errors are now displayed correctly in more cases
  - Console output now wraps at 120 characters instead of 80 characters
  - Console output is now buffered to reduce the number of OutputEvent messages sent from the host to the editor

- Added choice and input prompt support.  Prompts can be shown either through the console or natively
  in the editor via the `powerShell/showInputPrompt` and `powerShell/showChoicePrompt` requests.

#### New host command line parameters

- `/logLevel:<level>`: Sets the log output level for the host.  Possible values: `Verbose`, `Normal`, `Warning`, or `Error`.
- `/logPath:<path>`: Sets the output path for logs written while the host is running

#### Other improvements

- Initial work has been done to ensure support for PowerShell v3 and v4 APIs by compiling against the PowerShell
  reference assemblies that are published on NuGet. (thanks [@adamdriscoll](https://github.com/adamdriscoll)!)
- Initial WebSocket channel support (thanks [@adamdriscoll](https://github.com/adamdriscoll)!)
- Removed Nito.AsyncEx dependency

## 0.3.1
### Thursday, December 17, 2015

- Fixed issue PowerShell/vscode-powershell#49, Debug Console does not receive script output

## 0.3.0
### Tuesday, December 15, 2015

- First release with official NuGet packages!
  - [Microsoft.PowerShell.EditorServices](https://www.nuget.org/packages/Microsoft.PowerShell.EditorServices/) - Core .NET library
  - [Microsoft.PowerShell.EditorServices.Protocol](https://www.nuget.org/packages/Microsoft.PowerShell.EditorServices.Protocol/) - Protocol and client/server library
  - [Microsoft.PowerShell.EditorServices.Host](https://www.nuget.org/packages/Microsoft.PowerShell.EditorServices.Host/) - API host process package
- Introduced a new client/server API in the Protocol project which makes it
  much easier to write a client or server for the language and debugging services
- Introduced a new channel model which makes it much easier to add and consume
  new protocol channel implementations
- Major improvements in variables retrieved from the debugging service:
  - Global and script scope variables are now accessible
  - New "Autos" scope which shows only the variables defined within the current scope
  - Greatly improved representation of variable values, especially for dictionaries and
    objects that implement the ToString() method
- Added new "Expand Alias" command which resolves command aliases used in a file or
  selection and updates the source text with the resolved command names
- Reduced default Script Analyzer rules to a minimal list
- Improved startup/shutdown behavior and logging
- Fixed a wide array of completion text replacement bugs

## 0.2.0
### Monday, November 23, 2015

- Added Online Help command
- Enabled PowerShell language features for untitled and in-memory (e.g. in Git diff viewer) PowerShell files
- Fixed high CPU usage when completing or hovering over an application path

## 0.1.0
### Wednesday, November 18, 2015

Initial release with the following features:

- IntelliSense for cmdlets and more
- Rule-based analysis provided by PowerShell Script Analyzer
- Go to Definition of cmdlets and variables
- Find References of cmdlets and variables
- Document and workspace symbol discovery
- Local script debugging and basic interactive console support