# PowerShell Editor Services

[![Build Status](https://powershell.visualstudio.com/PowerShellEditorServices/_apis/build/status/PowerShellEditorServices-ci?branchName=master)](https://powershell.visualstudio.com/PowerShellEditorServices/_build/latest?definitionId=50&branchName=master)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/fb9129c327dc4f459ad2fd167d9a574f)](https://app.codacy.com/app/TylerLeonhardt/PowerShellEditorServices?utm_source=github.com&utm_medium=referral&utm_content=PowerShell/PowerShellEditorServices&utm_campaign=Badge_Grade_Dashboard)
[![Dependabot Status](https://api.dependabot.com/badges/status?host=github&repo=PowerShell/PowerShellEditorServices)](https://dependabot.com)
[![Discord](https://img.shields.io/discord/180528040881815552.svg?label=%23vscode&logo=discord&logoColor=white)](https://aka.ms/psdiscord)
[![Join the chat at https://gitter.im/PowerShell/PowerShellEditorServices](https://badges.gitter.im/PowerShell/PowerShellEditorServices.svg)](https://gitter.im/PowerShell/PowerShellEditorServices?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

PowerShell Editor Services is a PowerShell module that provides common
functionality needed to enable a consistent and robust PowerShell development
experience in almost any editor or integrated development environment (IDE).

## PowerShell [Language Server Protocol](https://microsoft.github.io/language-server-protocol/) clients using PowerShell Editor Services

The functionality in PowerShell Editor Services is already available in the following editor extensions:

-  [The VSCode PowerShell extension](https://github.com/PowerShell/vscode-powershell), also available in Azure Data Studio
-  [coc-powershell](https://github.com/yatli/coc-powershell), a vim/neovim PowerShell plugin
-  [The IntelliJ PowerShell plugin](https://github.com/ant-druha/intellij-powershell)
-  [lsp-powershell](https://github.com/kiennq/lsp-powershell), an Emacs PowerShell plugin

## Features

-  The Language Service provides common editor features for the PowerShell language:
  -  Code navigation actions (find references, go to definition)
  -  Statement completions (IntelliSense)
  -  Real-time semantic analysis of scripts using PowerShell Script Analyzer
-  The Debugging Service simplifies interaction with the PowerShell debugger (breakpoints, variables, call stack, etc)
-  The [$psEditor API](http://powershell.github.io/PowerShellEditorServices/guide/extensions.html) enables scripting of the host editor
-  A full, terminal-based Integrated Console experience for interactive development and debugging

## Usage

Looking to integrate PowerShell Editor Services into your [Language Server Protocol](https://microsoft.github.io/language-server-protocol/) compliant editor or client? We support two ways of connecting.

### Named Pipes/Unix Domain Sockets (recommended)

If you're looking for the more feature-rich experience,
Named Pipes are the way to go.
They give you all the benefit of the Lanaguge Server Protocol with extra capabilities that you can take advantage of:

-  The PowerShell Integrated Console
-  Debugging using the [Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/)

The typical command to start PowerShell Editor Services using named pipes is as follows:

```powershell
pwsh -NoLogo -NoProfile -Command "$PSES_BUNDLE_PATH/PowerShellEditorServices/Start-EditorServices.ps1 -BundledModulesPath $PSES_BUNDLE_PATH -LogPath $SESSION_TEMP_PATH/logs.log -SessionDetailsPath $SESSION_TEMP_PATH/session.json -FeatureFlags @() -AdditionalModules @() -HostName 'My Client' -HostProfileId 'myclient' -HostVersion 1.0.0 -LogLevel Normal"
```

> NOTE: In the example above,
>
> -  `$PSES_BUNDLE_PATH` is the root of the PowerShellEditorServices.zip downloaded from the GitHub releases.
> -  `$SESSION_TEMP_PATH` is the folder path that you'll use for this specific editor session.

Once the command is run,
PowerShell Editor Services will wait until the client connects to the Named Pipe.
The `session.json` will contain the paths of the Named Pipes that you will connect to.
There will be one you immediately connect to for Language Server Protocol messages,
and one you connect to when you launch the debugger for Debug Adapter Protocol messages.

The Visual Studio Code, Vim, and IntelliJ extensions currently use Named Pipes.

#### PowerShell Integrated Console

![image](https://user-images.githubusercontent.com/2644648/66245084-6985da80-e6c0-11e9-9c7b-4c8476190df5.png)

The PowerShell Integrated Console uses the host process' Stdio streams for console input and output. Please note that this is mutually exclusive from using Stdio for the language server protocol messages.

If you want to take advantage of the PowerShell Integrated Console which automatically shares state with the editor-side,
you must include the `-EnableConsoleRepl` switch when called `Start-EditorServices.ps1`.

This is typically used if your client has the ability to create arbirary terminals in the editor like below:

![integrated console in vscode](https://user-images.githubusercontent.com/2644648/66245018-04ca8000-e6c0-11e9-808c-b86144149444.png)

The Visual Studio Code, Vim, and IntelliJ extensions currently use the PowerShell Integrated Console.

#### Debugging

Debugging support is also exposed with PowerShell Editor Services.
It is handled within the same process as the language server protocol handing.
This provides a more integrated experience for end users but is something to note as not many other language servers work in this way.
If you want to take advantage of debugging,
your client must support the [Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/).
Your client should use the path to the debug named pipe found in the `session.json` file talked about above.

Currently only the Visual Studio Code extension supports debugging.

### Stdio

Stdio is a simpler and more universal mechanism when it comes to the Language Server Protocol and is what we recommend if your editor/client doesn't need to support the PowerShell Integrated Console or debugging.

> NOTE: Debugging and the Integrated Console are not features of the Stdio channel because each feature requires their own IO streams and since the Stdio model only provides a single set of streams (Stdio),
> these features cannot be leveraged.

The typical command to start PowerShell Editor Services using stdio is as follows:

```powershell
pwsh -NoLogo -NoProfile -Command "$PSES_BUNDLE_PATH/PowerShellEditorServices/Start-EditorServices.ps1 -BundledModulesPath $PSES_BUNDLE_PATH -LogPath $SESSION_TEMP_PATH/logs.log -SessionDetailsPath $SESSION_TEMP_PATH/session.json -FeatureFlags @() -AdditionalModules @() -HostName 'My Client' -HostProfileId 'myclient' -HostVersion 1.0.0 -Stdio -LogLevel Normal"
```

> NOTE: In the example above,
>
> -  `$PSES_BUNDLE_PATH` is the root of the PowerShellEditorServices.zip downloaded from the GitHub releases.
> -  `$SESSION_TEMP_PATH` is the folder path that you'll use for this specific editor session.

The important flag is the `-Stdio` flag which enables this communication protocol.

Currently, the Emacs extension uses Stdio.

### API Usage

Please note that we only consider the following as stable APIs that can be relied on:

-  Language server protocol connection
-  Debug adapter protocol connection
-  Start up mechanism

The types of PowerShell Editor Services can change at any moment and should not be linked against in production environment.

## Development

### 1. Install PowerShell 6+

Install PowerShell 6+ with [these instructions](https://github.com/PowerShell/PowerShell#get-powershell).

### 3. Clone the GitHub repository:

```
git clone https://github.com/PowerShell/PowerShellEditorServices.git
```

### 4. Install [Invoke-Build](https://github.com/nightroman/Invoke-Build)

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
contribute code, documentation, tests, or bug reports, please read our [Contribution Guide](http://powershell.github.io/PowerShellEditorServices/CONTRIBUTING.html) to learn more.

## Maintainers

-  [Keith Hill](https://github.com/rkeithhill) - [@r_keith_hill](http://twitter.com/r_keith_hill)
-  [Patrick Meinecke](https://github.com/SeeminglyScience) - [@SeeminglyScienc](http://twitter.com/SeeminglyScienc)
-  [Tyler Leonhardt](https://github.com/tylerleonhardt) - [@TylerLeonhardt](http://twitter.com/tylerleonhardt)
-  [Rob Holt](https://github.com/rjmholt) - [@rjmholt](https://twitter.com/rjmholt)

## License

This project is [licensed under the MIT License](LICENSE).

## [Code of Conduct][conduct-md]

This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code].
For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.

[conduct-code]: http://opensource.microsoft.com/codeofconduct/
[conduct-FAQ]: http://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com
[conduct-md]: https://github.com/PowerShell/PowerShellEditorServices/blob/master/CODE_OF_CONDUCT.md
