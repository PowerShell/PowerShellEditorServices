# PowerShell Editor Services Release History

## 1.5.0
### Friday, October 27, 2017

#### Fixes and Improvements

- [PowerShell/vscode-powershell #910](https://github.com/PowerShell/vscode-powershell/issues/910) -
  Set-VSCodeHtmlContentView cmdlet now exposes `JavaScriptPaths` and `StyleSheetPaths` parameters to allow using JavaScript code and CSS stylesheets in VS Code HTML preview views.

- [PowerShell/vscode-powershell #909](https://github.com/PowerShell/vscode-powershell/issues/909) -
  Write-VSCodeHtmlContentView's AppendBodyContent now accepts input from the pipeline

- [PowerShell/vscode-powershell #842](https://github.com/PowerShell/vscode-powershell/issues/842) -
  psedit can now open empty files in remote sessions

- [PowerShell/vscode-powershell #1040](https://github.com/PowerShell/vscode-powershell/issues/1040) -
  Non-PowerShell files opened in remote sessions using psedit can now be saved back to the remote server

- [PowerShell/vscode-powershell #625](https://github.com/PowerShell/vscode-powershell/issues/625) -
  Breakpoints are now cleared from the session when the debugger starts so that stale breakpoints from previous sessions are not hit

- [PowerShell/vscode-powershell #1004](https://github.com/PowerShell/vscode-powershell/issues/1004) -
  Handle exception case when finding references of a symbol

- [PowerShell/vscode-powershell #942](https://github.com/PowerShell/vscode-powershell/issues/942) -
  Temporary debugging session now does not hang when running "PowerShell Interactive Session" debugging configuration in VS Code

- [PowerShell/vscode-powershell #872](https://github.com/PowerShell/vscode-powershell/issues/872) -
  Watch variables with children are now expandable

- [PowerShell/PowerShellEditorServices #342](https://github.com/PowerShell/PowerShellEditorServices/issues/342) -
  Unexpected file URI schemes are now handled more reliably

- [PowerShell/PowerShellEditorServices #396](https://github.com/PowerShell/PowerShellEditorServices/issues/396) -
  Resolved errors being written to Integrated Console when running native applications while transcription is turned on

- [PowerShell/PowerShellEditorServices #529](https://github.com/PowerShell/PowerShellEditorServices/issues/529) -
  Fixed an issue with loading the PowerShellEditorServices module in PowerShell Core 6.0.0-beta3

- [PowerShell/PowerShellEditorServices #533](https://github.com/PowerShell/PowerShellEditorServices/pull/533)  -
  Added new $psEditor.GetCommand() method for getting all registered editor commands.  Thanks to [Kamil Kosek](https://github.com/kamilkosek)!

- [PowerShell/PowerShellEditorServices #535](https://github.com/PowerShell/PowerShellEditorServices/pull/535)  -
  Type information is now exposed on hover for variables in the Variables view

## 1.4.1
### Thursday, June 22, 2017

- [PowerShell/PowerShellEditorServices#529](https://github.com/PowerShell/PowerShellEditorServices/issues/529) -
  Fixed an issue with loading the PowerShellEditorServices module in PowerShell Core 6.0.0-beta3

## 1.4.0
### Wednesday, June 21, 2017

- [#517](https://github.com/PowerShell/PowerShellEditorServices/pull/517) -
  Added new `$psEditor.Workspace.NewFile()` API for creating a new untitled file
  in the host editor.  Thanks [Doug Finke](https://github.com/dfinke)!

- [#520](https://github.com/PowerShell/PowerShellEditorServices/pull/520) -
  Added a new PowerShellEditorServices.VSCode module to contain functionality
  that will only appear in Visual Studio Code.

- [#523](https://github.com/PowerShell/PowerShellEditorServices/pull/523) -
  Added APIs and cmdlets for creating custom HTML content views in VS Code.
  See the *-VSCodeHtmlContentView cmdlets for more information.

- [#516](https://github.com/PowerShell/PowerShellEditorServices/pull/516) -
  Code formatting using PSScriptAnalyzer has now been moved server-side to use
  the standard textDocument/formatting and textDocument/rangeFormatting message
  types

- [#521](https://github.com/PowerShell/PowerShellEditorServices/pull/521) -
  Code formatting now accepts 3 code formatting presets, "Stroustrup", "Allman",
  and "OTBS" which correspond to the most common PowerShell formatting styles.

- [#518](https://github.com/PowerShell/PowerShellEditorServices/pull/518) -
  Added `-DebugServiceOnly` parameter to `Start-EditorServicesHost` which enables
  launching an Editor Services session purely for debugging PowerShell code.

- [#519](https://github.com/PowerShell/PowerShellEditorServices/pull/519) -
  Added a Diagnostic logging level for the most verbose logging output which
  isn't always necessary for investigating issues.  The logging of JSON message
  bodies has been moved to this logging level.

## 1.3.2
### Monday, June 12, 2017

- [PowerShell/vscode-powershell#857](https://github.com/PowerShell/vscode-powershell/issues/855) - Typing a new function into a file no longer causes the language server to crash

- [PowerShell/vscode-powershell#855](https://github.com/PowerShell/vscode-powershell/issues/855) - "Format Document" no longer hangs indefinitely

- [PowerShell/vscode-powershell#859](https://github.com/PowerShell/vscode-powershell/issues/859) - Language server no longer hangs when opening a Pester test file containing dot-sourced script references

- [PowerShell/vscode-powershell#856](https://github.com/PowerShell/vscode-powershell/issues/856) - CodeLenses for function definitions no longer count the definition itself as a reference and shows "0 references" when there are no uses of that function

- [PowerShell/vscode-powershell#838](https://github.com/PowerShell/vscode-powershell/issues/838) - Right-clicking a debugger variable and selecting "Add to Watch" now has the desired result

- [PowerShell/vscode-powershell#837](https://github.com/PowerShell/vscode-powershell/issues/837) - Debugger call stack now navigates correctly to the user's selected stack frame

- [PowerShell/vscode-powershell#862](https://github.com/PowerShell/vscode-powershell/issues/862) - Terminating errors in the language server now close the Integrated Console immediately and prompt the user to restart the session

- [PowerShell/PowerShellEditorServices#505](https://github.com/PowerShell/PowerShellEditorServices/issues/505) - Added improved cmdlet help in the PowerShellEditorServices.Commands module

- [PowerShell/PowerShellEditorServices#509](https://github.com/PowerShell/PowerShellEditorServices/issues/509) - Importing the PowerShellEditorServices.Commands module no longer causes errors to be written about missing help languages

## 1.3.1
### Friday, June 9, 2017

#### Fixes and improvements

- [PowerShell/vscode-powershell#850](https://github.com/PowerShell/vscode-powershell/issues/850) -
  Fixed an issue where lower-cased "describe" blocks were not identified by
  the CodeLens feature.

- [PowerShell/vscode-powershell#851](https://github.com/PowerShell/vscode-powershell/issues/851) -
  Fixed an issue where the language server would hang when typing out a describe
  block.

- [PowerShell/vscode-powershell#852](https://github.com/PowerShell/vscode-powershell/issues/852) -
  Fixed an issue where Pester test names would not be detected correctly when
  other arguments like -Tags were being used on a Describe block.

## 1.3.0
### Friday, June 9, 2017

#### Notice of new internal redesign ([#484](https://github.com/PowerShell/PowerShellEditorServices/pull/484), [#488](https://github.com/PowerShell/PowerShellEditorServices/pull/488), [#489](https://github.com/PowerShell/PowerShellEditorServices/pull/489))

This release marks the start of a major redesign of the core PowerShell
Editor Services APIs, PSHost implementation, and service model.  Most of
these changes will be transparent to the language and debugging services
so there shouldn't be any major breaking changes.

The goal is to quickly design and validate a new extensibility model that
allows IFeatureProvider implementations to extend focused feature components
which could be a part of PowerShell Editor Services or another extension
module.  As we progress, certain features may move out of the core Editor
Services module into satellite modules.  This will allow our functionality
to be much more flexible and provide extensions with the same set of
capabilities that built-in features have.

We are moving toward a 2.0 release of the core PowerShell Editor Services
APIs over the next few months once this new design has been validated and
stabilized.  We'll produce updated API documentation as we move closer
to 2.0.

#### New document symbol and CodeLens features ([#490](https://github.com/PowerShell/PowerShellEditorServices/pull/490), [#497](https://github.com/PowerShell/PowerShellEditorServices/pull/497), [#498](https://github.com/PowerShell/PowerShellEditorServices/pull/498))

As part of our new extensibility model work, we've added two new components
which follow the new "feature and provider" model which we'll be moving
all other features to soon.

The IDocumentSymbols feature component provides a list of symbols for a
given document.  It relies on the results provided by a collection of
IDocumentSymbolProvider implementations which can come from any module.
We've added the following built-in IDocumentSymbolProvider implementations:

- ScriptDocumentSymbolProvider: Provides symbols for function and command
  definitions in .ps1 and .psm1 files
- PsdDocumentSymbolProvider: Provides symbols for keys in .psd1 files
- PesterDocumentSymbolProvider: Provides symbols for Describe, Context, and
  It blocks in Pester test scripts

We took a similar approach to developing an ICodeLenses feature component
which retrieves a list of CodeLenses which get displayed in files to provide
visible actions embedded into the code.  We used this design to add the
following built-in ICodeLensProvider implementations:

- ReferencesCodeLensProvider: Shows CodeLenses like "3 references" to indicate
  the number of references to a given function or command
- PesterCodeLensProvider: Shows "Run tests" and "Debug tests" CodeLenses on
  Pester Describe blocks in test script files allowing the user to easily
  run and debug those tests

Note that the ICodeLensProvider and IDocumentSymbolProvider interfaces are
not fully stable yet but we encourage you to try using them so that you can
give us your feedback!

#### Added a new PowerShellEditorServices.Commands module (#[487](https://github.com/PowerShell/PowerShellEditorServices/pull/487), #[496](https://github.com/PowerShell/PowerShellEditorServices/pull/496))

We've added a new Commands module that gets loaded inside of PowerShell Editor
Services to provide useful functionality when the $psEditor API is available.

Thanks to our new co-maintainer [Patrick Meinecke](https://github.com/SeeminglyScience),
we've gained a new set of useful commands for interacting with the $psEditor APIs
within the Integrated Console:

- [Find-Ast](https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/docs/Find-Ast.md)
- [Get-Token](https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/docs/Get-Token.md)
- [ConvertFrom-ScriptExtent](https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/docs/ConvertFrom-ScriptExtent.md)
- [ConvertTo-ScriptExtent](https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/docs/ConvertTo-ScriptExtent.md)
- [Set-ScriptExtent](https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/docs/Set-ScriptExtent.md)
- [Join-ScriptExtent](https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/docs/Join-ScriptExtent.md)
- [Test-ScriptExtent](https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/docs/Test-ScriptExtent.md)
- [Import-EditorCommand](https://github.com/PowerShell/PowerShellEditorServices/blob/master/module/docs/Import-EditorCommand.md)

#### Microsoft.PowerShell.EditorServices API removals ([#492](https://github.com/PowerShell/PowerShellEditorServices/pull/492))

We've removed the following classes and interfaces which were previously
considered public APIs in the core Editor Services assembly:

- ConsoleService and IConsoleHost: We now centralize our host interface
  implementations under the standard PSHostUserInterface design.
- IPromptHandlerContext: We no longer have the concept of "prompt handler
  contexts."  Each PSHostUserInterface implementation now has one way of
  displaying console-based prompts to the user.  New editor window prompting
  APIs will be added for the times when a UI is needed.
- Logger: now replaced by a new non-static ILogger instance which can be
  requested by extensions through the IComponentRegistry.

## 1.2.1
### Thursday, June 1, 2017

#### Fixes and improvements

- [#478](https://github.com/PowerShell/PowerShellEditorServices/issues/478) -
  Dynamic comment help snippets now generate parameter fields correctly
  when `<#` is typed above a `param()` block.

- [PowerShell/vscode-powershell#808](https://github.com/PowerShell/vscode-powershell/issues/808) -
  An extra `PS>` is no longer being written to the Integrated Console for
  some users who have custom prompt functions.

- [PowerShell/vscode-powershell#813](https://github.com/PowerShell/vscode-powershell/issues/813) -
  Finding references of symbols across the workspace now properly handles
  inaccessible folders and file paths

- [PowerShell/vscode-powershell#821](https://github.com/PowerShell/vscode-powershell/issues/821) -
  Note properties on PSObjects are now visible in the debugger's Variables
  view

## 1.2.0
### Wednesday, May 31, 2017

#### Fixes and improvements

- [#462](https://github.com/PowerShell/PowerShellEditorServices/issues/462) -
  Fixed crash when getting signature help for functions and scripts
  using invalid parameter attributes

- [PowerShell/vscode-powershell#763](https://github.com/PowerShell/vscode-powershell/issues/763) -
  Dynamic comment-based help snippets now work inside functions

- [PowerShell/vscode-powershell#710](https://github.com/PowerShell/vscode-powershell/issues/710) -
  Variable definitions can now be found across the workspace

- [PowerShell/vscode-powershell#771](https://github.com/PowerShell/vscode-powershell/issues/771) -
  Improved dynamic comment help snippet performance in scripts with many functions

- [PowerShell/vscode-powershell#774](https://github.com/PowerShell/vscode-powershell/issues/774) -
  Pressing Enter now causes custom prompt functions to be fully evaluated

- [PowerShell/vscode-powershell#770](https://github.com/PowerShell/vscode-powershell/issues/770) -
  Fixed issue where custom prompt function might be written twice when
  starting the integrated console

## 1.1.0
### Thursday, May 18, 2017

#### Fixes and improvements

- [#452](https://github.com/PowerShell/PowerShellEditorServices/pull/452) -
  Added the `powerShell/getCommentHelp` request type for requesting a snippet-style
  text edit to add comment-based help to a function defined at a particular location.

- [#455](https://github.com/PowerShell/PowerShellEditorServices/pull/455) -
  Added the `powerShell/startDebugger` notification type to notify the editor that it
  should activate its debugger because a breakpoint has been hit in the session while
  no debugger client was attached.

- [#663](https://github.com/PowerShell/vscode-powershell/issues/663) and [#689](https://github.com/PowerShell/vscode-powershell/issues/689) -
  We now write the errors and Write-Output calls that occur while loading profile
  scripts so that it's easier to diagnose issues with your profile scripts.

## 1.0.0
### Wednesday, May 10, 2017

We are excited to announce that we've reached version 1.0!  For more information,
please see the [official announcement](https://blogs.msdn.microsoft.com/powershell/2017/05/10/announcing-powershell-for-visual-studio-code-1-0/)
on the PowerShell Team Blog.

#### Fixes and improvements

- Upgraded our Language Server Protocol support to [protocol version 3](https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md).

- Added basic module-wide function references support which searches all of the
  PowerShell script files within the current workspace for references and
  definitions.

- Fixed [vscode-powershell #698](https://github.com/PowerShell/vscode-powershell/issues/698) -
  When debugging scripts in the integrated console, the cursor position should now
  be stable after stepping through your code!  Please let us know if you see any
  other cases where this issue appears.

- Fixed [vscode-powershell #626](https://github.com/PowerShell/vscode-powershell/issues/626) -
  Fixed an issue where debugging a script in one VS Code window would cause that script's
  output to be written to a different VS Code window in the same process.

- Fixed [vscode-powershell #618](https://github.com/PowerShell/vscode-powershell/issues/618) -
  Pressing enter on an empty command line in the Integrated Console no longer adds the
  empty line to the command history.

- Fixed [vscode-powershell #617](https://github.com/PowerShell/vscode-powershell/issues/617) -
  Stopping the debugger during a prompt for a mandatory script parameter no
  longer crashes the language server.

- Fixed [#428](https://github.com/PowerShell/PowerShellEditorServices/issues/428) -
  Debugger no longer hangs when you stop debugging while an input or choice prompt is
  active in the integrated console.

## 0.12.1
### Friday, April 7, 2017

- Fixed [vscode-powershell #645](https://github.com/PowerShell/vscode-powershell/issues/645) -
  "Go to Definition" or "Find References" now work in untitled scripts without
  crashing the session
- Fixed [vscode-powershell #632](https://github.com/PowerShell/vscode-powershell/issues/632) -
  Debugger no longer hangs when launched while PowerShell session is still
  initializing
- Fixed [#430](https://github.com/PowerShell/PowerShellEditorServices/issues/430) -
  Resolved occasional IntelliSense slowness by preventing the implicit loading
  of the PowerShellGet and PackageManagement modules.  This change will be reverted
  once a bug in PackageManagement is fixed.
- Fixed [#427](https://github.com/PowerShell/PowerShellEditorServices/issues/427) -
  Fixed an occasional crash when requesting editor IntelliSense while running
  a script in the debugger
- Fixed [#416](https://github.com/PowerShell/PowerShellEditorServices/issues/416) -
  Cleaned up errors that would appear in the `$Errors` variable from the use
  of `Get-Command` and `Get-Help` in IntelliSense results

## 0.12.0
### Tuesday, April 4, 2017

#### Fixes and improvements

- Added [#408](https://github.com/PowerShell/PowerShellEditorServices/pull/408) -
  Enabled debugging of untitled script files

- Added [#405](https://github.com/PowerShell/PowerShellEditorServices/pull/405) -
  Script column number is now reported in the top stack frame when the debugger
  stops to aid in displaying a column indicator in Visual Studio Code

- Fixed [#411](https://github.com/PowerShell/PowerShellEditorServices/issues/411) -
  Commands executed internally now interrupt the integrated console's command
  prompt

- Fixed [#409](https://github.com/PowerShell/PowerShellEditorServices/pull/409) -
  PowerShell session now does not crash when a breakpoint is hit outside of
  debug mode

- Fixed [#614](https://github.com/PowerShell/vscode-powershell/issues/614) -
  Auto variables are now populating correctly in the debugger.  **NOTE**: There is
  a known issue where all of a script's variables begin to show up in the
  Auto list after running a script for the first time.  This is caused by
  a change in 0.11.0 where we now dot-source all debugged scripts.  We will
  provide an option for this behavior in the future

## 0.11.0
### Wednesday, March 22, 2017

#### Fixes and improvements

- Added [PowerShell/vscode-powershell#583](https://github.com/PowerShell/vscode-powershell/issues/583) -
  When you open files in a remote PowerShell session with the `psedit` command,
  their updated contents are now saved back to the remote machine when you save
  them in the editor.
- Added [PowerShell/vscode-powershell#540](https://github.com/PowerShell/vscode-powershell/issues/540) -
  The scripts that you debug are now dot-sourced into the integrated console's
  session, allowing you to experiment with the results of your last execution.
- Added [PowerShell/vscode-powershell#600](https://github.com/PowerShell/vscode-powershell/issues/600) -
  Debugger commands like `stepInto`, `continue`, and `quit` are now available
  in the integrated console while debugging a script.
- Fixed [PowerShell/vscode-powershell#533](https://github.com/PowerShell/vscode-powershell/issues/533) -
  The backspace key now works in the integrated console on Linux and macOS.  This
  fix also resolves a few usability problems with the integrated console on all
  supported OSes.
- Fixed [PowerShell/vscode-powershell#542](https://github.com/PowerShell/vscode-powershell/issues/542) -
  Get-Credential now hides keystrokes correctly on Linux and macOS.
- Fixed [PowerShell/vscode-powershell#579](https://github.com/PowerShell/vscode-powershell/issues/579) -
  Sorting of IntelliSense results is now consistent with the PowerShell ISE
- Fixed [PowerShell/vscode-powershell#575](https://github.com/PowerShell/vscode-powershell/issues/575) -
  The interactive console no longer starts up with errors in the `$Error` variable.

## 0.10.1
### Thursday, March 16, 2017

#### Fixes and improvements

- Fixed [#387](https://github.com/PowerShell/PowerShellEditorServices/issues/387) -
  Write-(Warning, Verbose, Debug) are missing message prefixes and foreground colors
- Fixed [#382](https://github.com/PowerShell/PowerShellEditorServices/issues/382) -
  PSHostUserInterface implementation should set SupportsVirtualTerminal to true
- Fixed [#192](https://github.com/PowerShell/PowerShellEditorServices/issues/192) -
  System-wide ExecutionPolicy of Bypass causes host process crash

## 0.10.0
### Tuesday, March 14, 2017

These improvements are described in detail in the [vscode-powershell changelog](https://github.com/PowerShell/vscode-powershell/blob/master/CHANGELOG.md#0100)
for its 0.10.0 release.

#### Language feature improvements

- Added new terminal-based integrated console
- Added new code formatting settings with additional rules
- Added Get-Credential, SecureString, and PSCredential support

#### Debugging improvements

- Connected primary debugging experience with integrated console
- Added column number breakpoints
- Added support for step-in debugging of remote ScriptBlocks with [PowerShell Core 6.0.0-alpha.17](https://github.com/PowerShell/PowerShell/releases/tag/v6.0.0-alpha.17)

## 0.9.0
### Thursday, January 19, 2017

These improvements are described in detail in the [vscode-powershell changelog](https://github.com/PowerShell/vscode-powershell/blob/master/CHANGELOG.md#090)
for its 0.9.0 release.

#### Language feature improvements

- Added PowerShell code formatting integration with PSScriptAnalyzer
- Improved PSScriptAnalyzer execution, now runs asynchronously
- Added Document symbol support for .psd1 files

#### Debugging improvements

- Remote session and debugging support via Enter-PSSession (PowerShell v4+)
- "Attach to process/runspace" debugging support via Enter-PSHostProcess and Debug-Runspace (PowerShell v5+)
- Initial `psedit` command support for loading files from remote sessions
- Added language server protocol request for gathering PowerShell host processes for debugging
- Many minor improvements to the debugging experience

#### $psEditor API improvements

- Added `FileContext.Close()` method to close an open file

#### Other fixes and improvements

- Fixed [#339](https://github.com/PowerShell/PowerShellEditorServices/issues/339):
  Prompt functions that return something other than string cause the debugger to crash

## 0.8.0
### Friday, December 16, 2016

#### Language feature improvements

- Added support for "suggested corrections" from PSScriptAnalyzer
- Added support for getting and setting the active list of PSScriptAnalyzer
  rules in the editing session
- Enabled the user of PSScriptAnalyzer in the language service on PowerShell
  versions 3 and 4
- Added PSHostUserInterface support for IHostUISupportsMultipleChoiceSelection

#### $psEditor API improvements

- Added $psEditor.Workspace
- Added $psEditor.Window.Show[Error, Warning, Information]Message methods for
  showing messages in the editor UI
- Added $psEditor.Workspace.Path to provide access to the workspace path
- Added $psEditor.Workspace.GetRelativePath to resolve an absolute path
  to a workspace-relative path
- Added FileContext.WorkspacePath to get the workspace-relative path of
  the file

#### Debugging improvements

- Enabled setting variable values from the debug adapter protocol
- Added breakpoint hit count support invthe debug service

#### Other improvements

- Added a new TemplateService for integration with Plaster
- Refactored PSScriptAnalyzer integration to not take direct dependency
  on the .NET assembly

#### Bug fixes

- Fixed #138: Debugger output was not being written for short scripts
- Fixed #242: Remove timeout for PSHostUserInterface prompts
- Fixed #237: Set session's current directory to the workspace path
- Fixed #312: File preview Uris crash the language server
- Fixed #291: Dot-source reference detection should ignore ScriptBlocks

## 0.7.2
### Friday, September 2, 2016

- Fixed #284: PowerShellContext.AbortException crashes when called more than once
- Fixed #285: PSScriptAnalyzer settings are not being passed to Invoke-ScriptAnalyzer
- Fixed #287: Language service crashes when invalid path chars are used in dot-sourced script reference

## 0.7.1
### Tuesday, August 23, 2016

- Fixed PowerShell/vscode-powerShell#246: Restore default PSScriptAnalyzer ruleset
- Fixed PowerShell/vscode-powershell#248: Extension fails to load on Windows 7 with PowerShell v3

## 0.7.0
### Thursday, August 18, 2016

#### Introducing support for Linux and macOS!

This release marks the beginning of our support for Linux and macOS via
the new [cross-platform release of PowerShell](https://github.com/PowerShell/PowerShell).

NuGet packages will be provided in the upcoming 0.7.1 release.

#### Other improvements

- Introduced a new TCP channel to provide a commonly-available communication channel
  across multiple editors and platforms
- PowerShell Script Analyzer integration has been shifted from direct use via DLL to
  consuming the PowerShell module and cmdlets
- Updated code to account for platform differences across Windows and Linux/macOS
- Improved stability of the language service when being used in Sublime Text

## 0.6.2
### Tuesday, August 9, 2016

- Fixed #264: Variable and parameter IntelliSense broken in VS Code 1.4.0
- Fixed #240: Completion item with regex metachars can cause editor host to crash
- Fixed #232: Language server sometimes crashes then $ErrorActionPreference = "Stop"

## 0.6.1
### Monday, May 16, 2016

- Fixed #221: Language server sometimes fails to initialize preventing IntelliSense, etc from working
- Fixed #222: Editor commands are not receiving $host.UI prompt results

## 0.6.0
### Thursday, May 12, 2016

#### Introduced a new documentation site

- We have launched a new [documentation site](https://powershell.github.io/PowerShellEditorServices/)
  for this project on GitHub Pages.  This documentation provides both a user guide
  and .NET API documentation pages that are generated directly from our code
  documentation.  Check it out and let us know what you think!

#### Added a new cross-editor extensibility model

- We've added a new extensibility model which allows you to write PowerShell
  code to add new functionality to Visual Studio Code and other editors with
  a single API.  If you've used `$psISE` in the PowerShell ISE, you'll feel
  right at home with `$psEditor`.  Check out the [documentation](https://powershell.github.io/PowerShellEditorServices/guide/extensions.html)
  for more details!

#### Support for user and system-wide profiles

- We've now introduced the `$profile` variable which contains the expected
  properties that you normally see in `powershell.exe` and `powershell_ise.exe`:
  - `AllUsersAllHosts`
  - `AllUsersCurrentHost`
  - `CurrentUserAllHosts`
  - `CurrentUserCurrentHost`
- Each editor integration can specify what their host-specific profile filename
  should be.  If no profile name has been specified a default of `PowerShellEditorServices_profile.ps1`
  is used.
- Profiles are not loaded by default when PowerShell Editor Services is used.
  This behavior may change in the future based on user feedback.
- Editor integrations can also specify their name and version for the `$host.Name`
  and `$host.Version` properties so that script authors have a better idea of
  where their code is being used.

#### Other improvements

- `$env` variables now have IntelliSense complete correctly (#206).
- The debug adapter now does not crash when you attempt to add breakpoints
  for files that have been moved or don't exist (#195).
- Fixed an issue preventing output from being written in the debugger if you
  don't set a breakpoint before running a script.
- Debug adapter now doesn't crash when rendering an object for the
  variables view if ToString throws an exception.

## 0.5.0
### Thursday, March 10, 2016

#### Support for PowerShell v3 and v4

- Support for PowerShell v3 and v4 is now complete!  Note that for this release,
  Script Analyzer support has been disabled for PS v3 and v4 until we implement
  a better strategy for integrating it as a module dependency

#### Debugging improvements

- Added support for command breakpoints
- Added support for conditional breakpoints
- Improved the debug adapter startup sequence to handle new VS Code debugging features

#### Other improvements

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