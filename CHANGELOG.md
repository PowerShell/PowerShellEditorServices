# PowerShell Editor Services Release History

## v3.4.2
### Friday, May 20, 2022

- ✨ 🧠 [PowerShellEditorServices #1809](https://github.com/PowerShell/PowerShellEditorServices/pull/1809) - Additional IntelliSense fixes and ToolTip overhaul.

## v3.4.1
### Thursday, May 19, 2022

- 🐛 🛫 [PowerShellEditorServices #1807](https://github.com/PowerShell/PowerShellEditorServices/pull/1807) - Fix startup bug when zero profiles are present.

## v3.4.0
### Tuesday, May 17, 2022

- 🐛 🔍 [vscode-powershell #3965](https://github.com/PowerShell/PowerShellEditorServices/pull/1804) - Wrap untitled script with newlines.
- 🐛 🔍 [vscode-powershell #3980](https://github.com/PowerShell/PowerShellEditorServices/pull/1803) - Fix execution of debug prompt commands.
- 🐛 📟 [PowerShellEditorServices #1802](https://github.com/PowerShell/PowerShellEditorServices/pull/1802) - Set `EnableProfileLoading` default to `true`.
- 🐛 🙏 [PowerShellEditorServices #1695](https://github.com/PowerShell/PowerShellEditorServices/pull/1801) - Re-enable stdio clients by fixing initialization sequence.
- ✨ 🧠 [PowerShellEditorServices #1799](https://github.com/PowerShell/PowerShellEditorServices/pull/1799) - Fix a lot of IntelliSense issues.
- #️⃣ 🙏 [vscode-powershell #3962](https://github.com/PowerShell/PowerShellEditorServices/pull/1797) - Increase stack size for PowerShell 5. (Thanks @nohwnd!)

## v3.3.5
### Thursday, May 05, 2022

- 🐛 🔍 [vscode-powershell #3950](https://github.com/PowerShell/PowerShellEditorServices/pull/1791) - Send `sendKeyPress` event across DAP for temporary integrated consoles.

## v3.3.4
### Tuesday, May 03, 2022

- ✨ 🙏 [PowerShellEditorServices #1787](https://github.com/PowerShell/PowerShellEditorServices/pull/1787) - Bump PSReadLine to `v2.2.5`.

## v3.3.3
### Monday, May 02, 2022

- ✨ 📟 [PowerShellEditorServices #1785](https://github.com/PowerShell/PowerShellEditorServices/pull/1785) - Add `IHostUISupportsMultipleChoiceSelection` implementation.
- 🐛 🔍 [PowerShellEditorServices #1784](https://github.com/PowerShell/PowerShellEditorServices/pull/1784) - Do not exit from `DebuggerStop` unless resuming.

## v3.3.2
### Thursday, April 28, 2022

- 🐛 🛫 [PowerShellEditorServices #1782](https://github.com/PowerShell/PowerShellEditorServices/pull/1782) - Fix ordering of startup tasks so `psEditor` is defined before profiles are loaded.
- 🐛 📟 [PowerShellEditorServices #1781](https://github.com/PowerShell/PowerShellEditorServices/pull/1781) - Bring back `WriteWithPrompt()`.
- 🐛 📟 [vscode-powershell #3937](https://github.com/PowerShell/PowerShellEditorServices/pull/1779) - Update to latest PSReadLine beta (with fix for race condition).
- 🐛 🔍 [PowerShellEditorServices #1778](https://github.com/PowerShell/PowerShellEditorServices/pull/1778) - Fix extra prompting and manual debugger commands.
- ✨ 🚂 [PowerShellEditorServices #1777](https://github.com/PowerShell/PowerShellEditorServices/pull/1777) - Consolidate `InterruptCurrentForeground` and `MustRunInForeground`.
- ✨ 🚂 [PowerShellEditorServices #1776](https://github.com/PowerShell/PowerShellEditorServices/pull/1776) - Don't cancel on disposal of `CancellationScope`.

## v3.3.1
### Wednesday, April 20, 2022

- 🐛 👷 [PowerShellEditorServices #1761](https://github.com/PowerShell/PowerShellEditorServices/pull/1766) - Bump `net461` to `net462` due to upcoming end of support.
- 🐛 💎 [vscode-powershell #3928](https://github.com/PowerShell/PowerShellEditorServices/pull/1764) - Fix formatting handlers and PSScriptAnalyzer loading.
- 🐛 🔍 [PowerShellEditorServices #1762](https://github.com/PowerShell/PowerShellEditorServices/pull/1762) - Fix prompt spam and general debugger reliability improvements.
- ✨ 🙏 [PowerShellEditorServices #1479](https://github.com/PowerShell/PowerShellEditorServices/pull/1759) - Enable IDE0005 (unneccessary using statements) as error.
- 🐛 🙏 [PowerShellEditorServices #1754](https://github.com/PowerShell/PowerShellEditorServices/pull/1758) - With a fix in PSReadLine, we don't have to return a "null" key press.

## v3.3.0
### Monday, April 18, 2022

- #️⃣ 🙏 [PowerShellEditorServices #1757](https://github.com/PowerShell/PowerShellEditorServices/pull/1757) - Enable code analysis and formatting as errors on build.
- ✨ 🚂 [PowerShellEditorServices #1755](https://github.com/PowerShell/PowerShellEditorServices/pull/1755) - Apply automatic fixes (manually).
- 🐛 🔍 [PowerShellEditorServices #1736](https://github.com/PowerShell/PowerShellEditorServices/pull/1752) - Fix attach to process debugging.

## v3.2.0
### Tuesday, April 12, 2022

- 🐛 🙏 [PowerShellEditorServices #1751](https://github.com/PowerShell/PowerShellEditorServices/pull/1751) - Re-workaround uncancellable `Console.ReadKey`.
- 🐛 ‍🕵️ [PowerShellEditorServices #1749](https://github.com/PowerShell/PowerShellEditorServices/pull/1749) - Correctly map `SuggestedCorrection` to `MarkerCorrection`. (Thanks @bergmeister!)

## v3.1.6
### Thursday, March 24, 2022

- #️⃣ 🙏 [PowerShellEditorServices #1746](https://github.com/PowerShell/PowerShellEditorServices/pull/1746) - Replace `_consoleHostUI` with `_underlyingHostUI`.

## v3.1.5
### Thursday, March 10, 2022

- ✨ 🧠 [vscode-powershell #3364](https://github.com/PowerShell/PowerShellEditorServices/pull/1738) - Improve completion logic (more icons!).
- 🐛 🛫 [PowerShellEditorServices #1576](https://github.com/PowerShell/PowerShellEditorServices/pull/1735) - Remove `PackageManagement` module update prompt.
- 🐛 📟 [PowerShellEditorServices #1734](https://github.com/PowerShell/PowerShellEditorServices/pull/1734) - Finish redirection of `$Host.PrivateData`.
- 🐛 📟 [PowerShellEditorServices #1639](https://github.com/PowerShell/PowerShellEditorServices/pull/1732) - Redirect `PSHost.Notify*Application` methods.

## v3.1.4
### Thursday, February 24, 2022

- 🐛 🛫 [vscode-powershell #2658](https://github.com/PowerShell/PowerShellEditorServices/pull/1726) - Avoid error when `exclude` entry is a clause.
- 🐛 🚂 [vscode-powershell #3691](https://github.com/PowerShell/PowerShellEditorServices/pull/1725) - Fix editor commands to interrupt current prompt.
- ✨ 🔍 [PowerShellEditorServices #1724](https://github.com/PowerShell/PowerShellEditorServices/pull/1724) - Re-enable line breakpoints for untitled scripts.
- ✨ 🙏 [PowerShellEditorServices #1709](https://github.com/PowerShell/PowerShellEditorServices/pull/1723) - Update PSReadLine to 2.2.2.
- 🐛 📟 [vscode-powershell #3807](https://github.com/PowerShell/PowerShellEditorServices/pull/1719) - Reset progress messages at end of REPL.
- 🐛 ‍🕵️ [PowerShellEditorServices #1718](https://github.com/PowerShell/PowerShellEditorServices/pull/1718) - Return a code action for each diagnostic record. (Thanks @bergmeister!)

## v3.1.3
### Wednesday, February 16, 2022

- 🐛 🔍 [vscode-powershell #3832](https://github.com/PowerShell/PowerShellEditorServices/pull/1712) - Avoid stopping the debugger when canceling other tasks in a debug session.
- 🐛 📟 [PowerShellEditorServices #1607](https://github.com/PowerShell/PowerShellEditorServices/pull/1711) - Redirect `EditorServicesConsolePSHost.PrivateData` to `_internalHost`.
- 🐛 📟 [PowerShellEditorServices #1699](https://github.com/PowerShell/PowerShellEditorServices/pull/1710) - Handle edge case where `prompt` is undefined.
- 🐛 🔍 [PowerShellEditorServices #1704](https://github.com/PowerShell/PowerShellEditorServices/pull/1704) - Avoid recording debugger commands in the history.
- ✨ 🔍 [PowerShellEditorServices #1703](https://github.com/PowerShell/PowerShellEditorServices/pull/1703) - Use `static readonly` for default `ExecutionOptions`.
- 🐛 🔍 [vscode-powershell #3655](https://github.com/PowerShell/PowerShellEditorServices/pull/1702) - Fix running untitled scripts with arguments (but break line breakpoints) .
- ✨ 🙏 [PowerShellEditorServices #1694](https://github.com/PowerShell/PowerShellEditorServices/pull/1694) - Add `Thread.Sleep(100)` to throttle REPL when it's non-interactive. (Thanks @colinblaise!)

## v3.1.2
### Wednesday, February 02, 2022

- 🐛 📟 [vscode-powershell #3786](https://github.com/PowerShell/PowerShellEditorServices/pull/1691) - Print prompt and command when `WriteInputToHost` is true.
- 🐛 📟 [vscode-powershell #3685](https://github.com/PowerShell/PowerShellEditorServices/pull/1690) - Display prompt after `F8` finishes.
- 🐛 🔍 [vscode-powershell #3522](https://github.com/PowerShell/PowerShellEditorServices/pull/1685) - Synchronize PowerShell debugger and DAP server state.
- ✨ 🔍 [PowerShellEditorServices #1680](https://github.com/PowerShell/PowerShellEditorServices/pull/1680) - Display `DictionaryEntry` as key/value pairs in debugger. (Thanks @JustinGrote!)

## v3.1.1
### Monday, January 24, 2022

- #️⃣ 💎 [PowerShellEditorServices #1676](https://github.com/PowerShell/PowerShellEditorServices/pull/1676) - Use EditorConfig for dotnet build and suppress existing issues. (Thanks @JustinGrote!)
- 🐛 🔍 [PowerShellEditorServices #1672](https://github.com/PowerShell/PowerShellEditorServices/pull/1670) - Handle `debuggerResult` being null in `ProcessDebuggerResult`.
- 🐛 🙏 [PowerShellEditorServices #1663](https://github.com/PowerShell/PowerShellEditorServices/pull/1669) - Fix off-by-one error in validation within `GetOffsetAtPosition`.
- 🐛 📟 [PowerShellEditorServices #1667](https://github.com/PowerShell/PowerShellEditorServices/pull/1668) - Fix `Write-Host -NoNewLine` and `-*Color`. (Thanks @SeeminglyScience!)
- 🐛 🔍 [PowerShellEditorServices #1661](https://github.com/PowerShell/PowerShellEditorServices/pull/1664) - Fix `DebuggerSetsVariablesWithConversion` test.
- ✨ 🙏 [vscode-powershell #2800](https://github.com/PowerShell/PowerShellEditorServices/pull/1662) - Enable resolution of an alias to its function definition.
- ✨ 🔍 [PowerShellEditorServices #1633](https://github.com/PowerShell/PowerShellEditorServices/pull/1634) - Display `IEnumerables` and `IDictionaries` in debugger prettily (with "Raw View" available). (Thanks @JustinGrote!)

## v3.1.0
### Monday, January 10, 2022

So many more tests have been turned back on!

- ✨ 🙏 [PowerShellEditorServices #1658](https://github.com/PowerShell/PowerShellEditorServices/pull/1658) - Bump PSReadLine module to 2.2.0-beta5.
- 🐛 🚨 [PowerShellEditorServices #1444](https://github.com/PowerShell/PowerShellEditorServices/pull/1657) - Re-enable `ExtensionCommandTests.cs`.
- 🐛 🙏 [PowerShellEditorServices #1656](https://github.com/PowerShell/PowerShellEditorServices/pull/1656) - Resurrect support to resolve aliased references.
- 🐛 🚨 [PowerShellEditorServices #1445](https://github.com/PowerShell/PowerShellEditorServices/pull/1655) - Split and clean up `LanguageServiceTests.cs`.
- 🐛 🔍 [vscode-powershell #3715](https://github.com/PowerShell/PowerShellEditorServices/pull/1652) - Fix regression with `F5` to use `.` instead of `&` operator.
- ✨ 🚨 [vscode-powershell #3677](https://github.com/PowerShell/PowerShellEditorServices/pull/1651) - Enable `PsesInternalHostTests` (previously `PowerShellContextTests`).

## v2.5.3
### Wednesday, December 22, 2021

No changes. We're re-releasing to update signatures with a new certificate.

## v3.0.3
### Monday, December 20, 2021

- 🐛 🚂 [vscode-powershell #3718](https://github.com/PowerShell/PowerShellEditorServices/pull/1647) - Create `$psEditor` as a constant.
- #️⃣ 🙏 [PowerShellEditorServices #1641](https://github.com/PowerShell/PowerShellEditorServices/pull/1641) - Added check to see if `PSModulePath` already contained `BundledModulePath`. (Thanks @dkattan!)
- #️⃣ 🙏 [PowerShellEditorServices #1640](https://github.com/PowerShell/PowerShellEditorServices/pull/1640) - Implemented `-LanguageServiceOnly` switch. (Thanks @dkattan!)
- 🐛 🛫 [PowerShellEditorServices #1638](https://github.com/PowerShell/PowerShellEditorServices/pull/1638) - Fix `BundledModulePath` and PSReadLine loading (redux).
- 🐛 🔍 [PowerShellEditorServices #1635](https://github.com/PowerShell/PowerShellEditorServices/pull/1635) - Re-enable `DebugServiceTests` suite.

## v3.0.2
### Monday, November 22, 2021

- ✨ 📖 [PowerShellEditorServices #1631](https://github.com/PowerShell/PowerShellEditorServices/pull/1631) - Add Justin Grote as maintainer.
- 🐛 🔍 [vscode-powershell #3667](https://github.com/PowerShell/PowerShellEditorServices/pull/1630) - Improve debugger's variable population mechanism. (Thanks @JustinGrote and @SeeminglyScience!)
- 🐛 👷 [PowerShellEditorServices #1628](https://github.com/PowerShell/PowerShellEditorServices/pull/1628) - Fix build for Apple M1 when running PowerShell 7.2 (arm64).
- 🐛 👷 [PowerShellEditorServices #1626](https://github.com/PowerShell/PowerShellEditorServices/pull/1626) - Remove Windows Server 2016 from CI.
- ✨ 👷 [PowerShellEditorServices #1619](https://github.com/PowerShell/PowerShellEditorServices/pull/1619) - Install a single `dotnet` SDK.

## v3.0.1
### Wednesday, November 03, 2021

- 🐛 🔍 [PowerShellEditorServices #1608](https://github.com/PowerShell/PowerShellEditorServices/pull/1611) - Improve PowerShell command and argument escaping. (Thanks @JustinGrote!)
- 🐛 📟 [PowerShellEditorServices #1603](https://github.com/PowerShell/PowerShellEditorServices/pull/1606) - Add `LengthInBufferCells` back to `EditorServicesConsolePSHostRawUserInterface`.
- #️⃣ 🙏 [PowerShellEditorServices #1604](https://github.com/PowerShell/PowerShellEditorServices/pull/1604) - Fix profile loading and `$PROFILE` variable.

## v3.0.0
### Thursday, October 28, 2021

This preview release includes a complete overhaul of the core PowerShell engine
of PowerShell Editor Services.
This represents over a year's work,
tracked in [PSES #1295](https://github.com/PowerShell/PowerShellEditorServices/issues/1295)
and implemented in [PSES #1459](https://github.com/PowerShell/PowerShellEditorServices/pull/1459),
and is our answer to many, many issues
opened by users over the last few years.
We're hoping you'll see a marked improvement
in the reliability, performance and footprint
of the extension as a result.

Previously the Integrated Console was run
by setting threadpool tasks on a shared main runspace,
and where LSP servicing was done with PowerShell idle events.
This lead to overhead, threading issues
and a complex implementation intended to work around
the asymmetry between PowerShell as a synchronous,
single-threaded runtime and a language server
as an asynchronous, multi-threaded service.

Now, PowerShell Editor Services maintains its own dedicated pipeline thread,
which is able to service requests similar to JavaScript's event loop,
meaning we can run everything synchronously on the correct thread.
We also get more efficiency because we can directly call
PowerShell APIs and code written in C# from this thread,
without the overhead of a PowerShell pipeline.

This change has overhauled how we service LSP requests,
how the Integrated Console works,
how PSReadLine is integrated,
how debugging is implemented,
how remoting is handled,
and a long tail of other features in PowerShell Editor Services.

Also, in making it, while 6,000 lines of code were added,
we removed 12,000,
for a more maintainable, more efficient
and easier to understand extension backend.

While most of our testing has been re-enabled
(and we're working on adding more),
there are bound to be issues with this new implementation.
Please give this a try and let us know if you run into anything.

We also want to thank [@SeeminglyScience](https://github.com/SeeminglyScience)
for his help and knowledge as we've made this migration.

Finally, a crude breakdown of the work from the commits:

- An initial dedicated pipeline thread consumer implementation
- Implement the console REPL
- Implement PSRL idle handling
- Implement completions
- Move to invoking PSRL as a C# delegate
- Implement cancellation and <kbd>Ctrl</kbd>+<kbd>C</kbd>
- Make <kbd>F8</kbd> work again
- Ensure execution policy is set correctly
- Implement $PROFILE support
- Make nested prompts work
- Implement REPL debugging
- Implement remote debugging in the REPL
- Hook up the debugging UI
- Implement a new concurrent priority queue for PowerShell tasks
- Reimplement the REPL synchronously rather than on its own thread
- Really get debugging working...
- Implement DSC breakpoint support
- Reimplement legacy readline support
- Ensure stdio is still supported as an LSP transport
- Remove PowerShellContextService and other defunct code
- Get integration tests working again (and improve diagnosis of PSES failures)
- Get unit testing working again (except debug service tests)

## v2.5.2
### Monday, October 18, 2021

- ✨ 👷 [PowerShellEditorServices #1589](https://github.com/PowerShell/PowerShellEditorServices/pull/1589) - Remove `BinClean` dependency from build task. (Thanks @JustinGrote!)
- #️⃣ 🙏 [PowerShellEditorServices #1585](https://github.com/PowerShell/PowerShellEditorServices/pull/1585) - Setting to Disable Pester Code Lens. (Thanks @JustinGrote!)
- #️⃣ 🙏 [PowerShellEditorServices #1578](https://github.com/PowerShell/PowerShellEditorServices/pull/1578) - Fix typo in comments. (Thanks @glennsarti!)

## v2.5.1
### Tuesday, September 07, 2021

- 🐛 📟 [PowerShellEditorServices #24977523](https://github.com/PowerShell/PowerShellEditorServices/pull/1571) - Implement `LengthInBufferCells` to fix ANSI formatting. (Thanks @SeeminglyScience!)
- ✨ 🔍 [vscode-powershell #3522](https://github.com/PowerShell/PowerShellEditorServices/pull/1570) -  Send `stopDebugger` notification when appropriate.
- 🐛 🔍 [vscode-powershell #3537](https://github.com/PowerShell/PowerShellEditorServices/pull/1569) - Fix bug with `ExecuteScriptWithArgsAsync` when `script` is a command.

## v2.5.0
### Monday, August 30, 2021

- ✨ ‍🕵️ [PowerShellEditorServices #1562](https://github.com/PowerShell/PowerShellEditorServices/pull/1562) - Pin PSScriptAnalyzer to `1.20.0`, Plaster to `1.1.3` and PSReadLine to `2.1.0`.

## v2.4.9
### Monday, August 23, 2021

- 🐛 🔍 [vscode-powershell #3513](https://github.com/PowerShell/PowerShellEditorServices/pull/1555) - Fix debugger regression where console needed input to start/continue.

## v2.4.8
### Thursday, August 19, 2021

- 🐛 🛫 [PowerShellEditorServices #1547](https://github.com/PowerShell/PowerShellEditorServices/pull/1547) - Fix creation of `InitialSessionState` to use `CreateDefault2()`.
- ✨ 👷 [PowerShellEditorServices #1544](https://github.com/PowerShell/PowerShellEditorServices/pull/1546) - Explicitly disable implicit namespace imports.
- ✨ 👷 [PowerShellEditorServices #1545](https://github.com/PowerShell/PowerShellEditorServices/pull/1545) - Make `dotnet test` arguments configurable.
- 🐛 ⏱️ [vscode-powershell #3410](https://github.com/PowerShell/PowerShellEditorServices/pull/1542) - Add regression test for `System.Windows.Forms` bug.
- 🐛 👷 [PowerShellEditorServices #1541](https://github.com/PowerShell/PowerShellEditorServices/pull/1541) - Update C# language version to 10.0 to fix bug with .NET SDK 6 Preview 7.
- 🐛 🚨 [PowerShellEditorServices #1442](https://github.com/PowerShell/PowerShellEditorServices/pull/1540) - Fix tests in `Debugging/DebugServiceTests.cs` and simplify faulty script path logic.
- ✨ 🔍 [PowerShellEditorServices #1532](https://github.com/PowerShell/PowerShellEditorServices/pull/1532) - Make `ExecuteCommandAsync` cancellable .

## v2.4.7
### Tuesday, August 03, 2021

- ✨ 🔍 [PowerShellEditorServices #1533](https://github.com/PowerShell/PowerShellEditorServices/pull/1533) - Enable and fix many .NET Code Analysis warnings.
- ✨ 👷 [PowerShellEditorServices #1530](https://github.com/PowerShell/PowerShellEditorServices/pull/1530) - Update release and CI pipelines.
- ✨ 👷 [PowerShellEditorServices #1528](https://github.com/PowerShell/PowerShellEditorServices/pull/1528) - Automate entire release process.
- ✨ 🛫 [PowerShellEditorServices #1527](https://github.com/PowerShell/PowerShellEditorServices/pull/1527) - Add stack trace to resolve event handler on debug.
- ✨ 🛫 [PowerShellEditorServices #1523](https://github.com/PowerShell/PowerShellEditorServices/pull/1526) - Initialize runspaces with `InitialSessionState` object.

## v2.4.6
### Tuesday, July 13, 2021

- ✨ 🚨 [PowerShellEditorServices #1522](https://github.com/PowerShell/PowerShellEditorServices/pull/1522) - Run new PSReadLine test on Windows.
- ✨ 📖 [PowerShellEditorServices #1519](https://github.com/PowerShell/PowerShellEditorServices/pull/1519) - Update README.md. (Thanks @vladdoster!)
- ✨ 🙏 [PowerShellEditorServices #1493](https://github.com/PowerShell/PowerShellEditorServices/pull/1514) - Load only bundled `PSReadLine`.
- 🐛 👷 [PowerShellEditorServices #1513](https://github.com/PowerShell/PowerShellEditorServices/pull/1513) - Import `InvokePesterStub.ps1` from `vscode-powershell` (with history).
- 🐛 🛫 [PowerShellEditorServices #1503](https://github.com/PowerShell/PowerShellEditorServices/pull/1504) - Handle `incomingSettings` and `profileObject` being null. (Thanks @dkattan!)

## v2.4.5
### Wednesday, June 23, 2021

- 👷🐛 [PowerShellEditorServices #1509](https://github.com/PowerShell/PowerShellEditorServices/issues/1509) Fix signing of files in release.

## v2.4.4
### Wednesday, June 16, 2021

- 🐛 [PowerShellEditorServices #1495](https://github.com/PowerShell/PowerShellEditorServices/pull/1500) - Prevent some exceptions.
- #️⃣ 🙏 [vscode-powershell #3395](https://github.com/PowerShell/PowerShellEditorServices/pull/1494) - Work around `dotnet publish` bug.

## v2.4.3
### Wednesday, May 26, 2021

This stable release includes all the changes in the previews since v2.3.0, plus the following:

- ✨👷 [PowerShellEditorServices #1491](https://github.com/PowerShell/PowerShellEditorServices/pull/1491) - Bump OmniSharp to `v0.19.2`.
- 🧠🐛 [vscode-powershell #715](https://github.com/PowerShell/PowerShellEditorServices/pull/1484) - Fix unintentional module import. (Thanks @MartinGC94!)

The most significant change is the update to [OmniSharp
v0.19.2](https://github.com/OmniSharp/csharp-language-server-protocol/releases/tag/v0.19.2),
from the previous version v0.18.3, released in November 2020. OmniSharp is the underlying
Language Server Protocol and Debug Adapter Protocol server library, and as such is our
biggest dependency. This update brings us to the LSP 3.16 and DAP 1.48.x specifications,
enabling us to start incorporating all the latest LSP changes, and it includes numerous
bug fixes and enhancements resulting in a faster and more stable server and extension
experience.

## v2.4.2-preview.1
### Friday, May 21, 2021

- 🛫🐛 [vscode-powershell #3306](https://github.com/PowerShell/PowerShellEditorServices/pull/1481) - Bump OmniSharp to `v0.19.2-beta0002`.
- 💭✨ [PowerShellEditorServices #1474](https://github.com/PowerShell/PowerShellEditorServices/pull/1474) - Add more logging and internal documentation.
- 🚂✨ [PowerShellEditorServices #1467](https://github.com/PowerShell/PowerShellEditorServices/pull/1467) - Make code more explicit.
- 📖🐛 [PowerShellEditorServices #1465](https://github.com/PowerShell/PowerShellEditorServices/pull/1466) - Remove "All Rights Reserved" from copyright notices.
- 👷✨ [PowerShellEditorServices #1463](https://github.com/PowerShell/PowerShellEditorServices/pull/1464) - Enable CodeQL with `codeql-analysis.yml`.

## v2.4.1-preview.1
### Monday, April 26, 2021

- 🔍🐛 [PowerShellEditorServices #1460](https://github.com/PowerShell/PowerShellEditorServices/pull/1460) - Bump OmniSharp package to `0.19.2-beta0001`.
- 👷🐛 [PowerShellEditorServices #1455](https://github.com/PowerShell/PowerShellEditorServices/pull/1456) - Fix version in module definition file.

## v2.4.0-preview.1
### Friday, April 02, 2021

- 🧠✨ [PowerShellEditorServices #1176](https://github.com/PowerShell/PowerShellEditorServices/pull/1427) - Add '$' as trigger character for completion. (Thanks @MartinGC94!)
- 👷🚨✨ [PowerShellEditorServices #1426](https://github.com/PowerShell/PowerShellEditorServices/pull/1426) - Bump CI images and enable tests on Apple M1.
- ✨ [PowerShellEditorServices #1424](https://github.com/PowerShell/PowerShellEditorServices/pull/1424) - Update to use OmniSharp 0.19.0.
- #️⃣ 🙏 [vscode-powershell #3180](https://github.com/PowerShell/PowerShellEditorServices/pull/1411) - Fix New-EditorFile failing when no Editor window open. (Thanks @corbob!)

## v2.3.0
### Wednesday, February 24, 2021

- 👷 ✨ No changes, just releasing a stable version.

## v2.3.0-preview.4
### Tuesday, February 23, 2021

- 📖 🐛 [PowerShellEditorServices #1416](https://github.com/PowerShell/PowerShellEditorServices/pull/1416) -
  Fix some markdownlint errors in README.
- 🛫 🐛 [PowerShellEditorServices #1415](https://github.com/PowerShell/PowerShellEditorServices/pull/1415) -
  Fix configuration processing to ensure that profiles are loaded.

## v2.3.0-preview.3
### Friday, February 19, 2021

- 👷 ✨ [PowerShellEditorServices #1408](https://github.com/PowerShell/PowerShellEditorServices/pull/1408) -
  Rewrite release signing pipeline.
- 🚨 ✨ [PowerShellEditorServices #1398](https://github.com/PowerShell/PowerShellEditorServices/pull/1398) -
  Refactor e2e tests.
- 🚂 ✨ [PowerShellEditorServices #1381](https://github.com/PowerShell/PowerShellEditorServices/pull/1381) -
  Hook up Telemetry LSP event and add telemetry event when users opt-out/in to features.
- 👷 🐛 [PowerShellEditorServices #1397](https://github.com/PowerShell/PowerShellEditorServices/pull/1397) -
  More compliant NuGet.config.
- 📺 🐛 [vscode-powershell #3071](https://github.com/PowerShell/PowerShellEditorServices/pull/1394) -
  Fix #1393: Always use local help to return cmdlet help text. (Thanks @deadlydog!)
- 🚂 ✨ [PowerShellEditorServices #1376](https://github.com/PowerShell/PowerShellEditorServices/pull/1376) -
  Move to Omnisharp lib 0.18.x.
- 🛫 🐛 [vscode-powershell #2965](https://github.com/PowerShell/PowerShellEditorServices/pull/1363) -
  Fix error when started in low .NET versions.
- 📖 🐛 [PowerShellEditorServices #1364](https://github.com/PowerShell/PowerShellEditorServices/pull/1364) -
  Typos in README.md. (Thanks @robotboyfriend!)

## v2.3.0-preview.2
### Wednesday, September 16, 2020

- 🧠 ✨ [vscode-powershell #2898](https://github.com/PowerShell/PowerShellEditorServices/pull/1352) -
  Type and Namespace completions now have tooltips. (Thanks @AspenForester!)
- 🛫 🐛 [vscode-powershell #2719](https://github.com/PowerShell/PowerShellEditorServices/pull/1349) -
  Fix startup assembly version loading issue in PowerShell 6 and up.
- 🔗 🐛 [vscode-powershell #2810](https://github.com/PowerShell/PowerShellEditorServices/pull/1348) -
  Fix reference number on Windows due to directory separator difference on Windows.
- [PowerShellEditorServices #1343](https://github.com/PowerShell/PowerShellEditorServices/pull/1343) -
  Updated Semantic Handler to work with new LSP APIs. (Thanks @justinytchen!)
- [PowerShellEditorServices #1337](https://github.com/PowerShell/PowerShellEditorServices/pull/1337) -
  Treat `Member`s as `Properties` in Semantic Highlighting for better accuracy.

## v2.3.0-preview.1
### Thursday, July 30, 2020

- 📺✨ [PowerShellEditorServices #1328](https://github.com/PowerShell/PowerShellEditorServices/pull/1328) -
  Enable handlers for Semantic Highlighting for better highlighting accuracy.
- 👮✨ [PowerShellEditorServices #1333](https://github.com/PowerShell/PowerShellEditorServices/pull/1333) -
  Expose new rule PSAvoidUsingDoubleQuotesForConstantString added in PSScriptAnalyzer 1.19.1. (Thanks @bergmeister!)
- 📺✨ [PowerShellEditorServices #1321](https://github.com/PowerShell/PowerShellEditorServices/pull/1321) -
  Needed changes for Notebook UI Support.
- 🛫✨ [PowerShellEditorServices #1323](https://github.com/PowerShell/PowerShellEditorServices/pull/1323) -
  Add cwd property to settings. (Thanks @jwfx!)
- 🛫🐛 [PowerShellEditorServices #1317](https://github.com/PowerShell/PowerShellEditorServices/pull/1318) -
  Move tests to PS7 and PS7.1 and fix IsNetCore check.
- 🔗✨ [PowerShellEditorServices #1316](https://github.com/PowerShell/PowerShellEditorServices/pull/1316) -
  Return null when Hover request is cancelled or no symbol details. (Thanks @ralish!)
- 🛫🐛 [vscode-powershell #2763](https://github.com/PowerShell/PowerShellEditorServices/pull/1315) -
  TLS 1.2 Support When Installing PackageManagement Module. (Thanks @serkanz!)

## v2.2.0
### Thursday, June 11, 2020

- ⚡️🧠 Better performance of overall.
- ✨🛫 Support for ConstrainedLanguage mode.
- 🐛 Many squashed bugs
- ✨👮 Updated PSScriptAnalyzer to 1.19.0.
      - More formatting settings! (Thanks @bergmeister!)
- 📟 ✨ Updated PSReadLine to 2.0.2.
(From now on,
the stable extension will have the latest stable version of PSReadLine and the PowerShell Preview extension will have the latest preview of PSReadLine)

## v2.2.0-preview.4
### Monday, June 08, 2020

- 🔗 🐛 [PowerShellEditorServices #1304](https://github.com/PowerShell/PowerShellEditorServices/pull/1304) -
  Use From instead of FromFileSystem fixing CodeLens references.
- 📟 ✨ [PowerShellEditorServices #1290](https://github.com/PowerShell/PowerShellEditorServices/pull/1290) -
  Allow PSReadLine to work in ConstrainedLanguage mode.

## v2.2.0-preview.3
### Monday, June 01, 2020

- 🚂✨ [PowerShellEditorServices #1301](https://github.com/PowerShell/PowerShellEditorServices/pull/1301) -
  Fix `AddLanguageProtocolLogging` OmniSharp breaking change.
- 🚨✨ [PowerShellEditorServices #1298](https://github.com/PowerShell/PowerShellEditorServices/pull/1298) -
  Remove leftover csproj reference to already removed project in test project PowerShellEditorServices.Test.Host.csproj. (Thanks @bergmeister!)
- 🚂✨ [PowerShellEditorServices #1300](https://github.com/PowerShell/PowerShellEditorServices/pull/1300) -
  Address breaking changes in Omnisharp lib and depend on `DocumentUri` more.
- 🚂✨ [PowerShellEditorServices #1291](https://github.com/PowerShell/PowerShellEditorServices/pull/1291) -
  Depend on `DocumentUri` for handing vscode `Uri`'s.
- 🧠✨ [vscode-powershell #2706](https://github.com/PowerShell/PowerShellEditorServices/pull/1294) -
  Support `completionItem/resolve` request for comparison operators to show tooltip information.

## v2.2.0-preview.2
### Wednesday, May 13, 2020

- 🛫🐛 [PowerShellEditorServices #1288](https://github.com/PowerShell/PowerShellEditorServices/pull/1288) -
  Don't update PackageManagement on ConstrainedLanguage mode.
- 🛫🐛  [PowerShellEditorServices #1289](https://github.com/PowerShell/PowerShellEditorServices/pull/1289) -
  Fix startup on empty workspaces.
- 🛫🐛 [PowerShellEditorServices #1285](https://github.com/PowerShell/PowerShellEditorServices/pull/1285) -
  Use API on ScriptBlock to generate PSCommand to run in ConstrainedLanguage mode.
- ⚡️🧠 [PowerShellEditorServices #1283](https://github.com/PowerShell/PowerShellEditorServices/pull/1283) -
  Move to Omnisharp lib 0.17.0 for increased performance.
- ✨👮 [PowerShellEditorServices #1280](https://github.com/PowerShell/PowerShellEditorServices/pull/1280) -
  Add additional settings for PSScriptAnalyzer 1.19. (Thanks @bergmeister!)
- 🔗 🐛 [vscode-powershell #305](https://github.com/PowerShell/PowerShellEditorServices/pull/1279) -
  Fix document highlight column.
- 🐛🧠 [PowerShellEditorServices #1276](https://github.com/PowerShell/PowerShellEditorServices/pull/1276) -
  Handle when no CommandInfo comes back from Get-Command to prevent an Exception showing up in logs.

## v2.2.0-preview.1
### Tuesday, April 28, 2020

- 📟 🐛 [PowerShellEditorServices #1272](https://github.com/PowerShell/PowerShellEditorServices/pull/1272) -
  Allow progress colors to be settable and gettable from the internal host.
- 🛫 ✨ [PowerShellEditorServices #1239](https://github.com/PowerShell/PowerShellEditorServices/pull/1239) -
  Prompt to update PackageManagement when using an old version.
- 🛫 ✨ [PowerShellEditorServices #1269](https://github.com/PowerShell/PowerShellEditorServices/pull/1269) -
  Support ConstrainedLanguage mode.
- 📺 ✨ [PowerShellEditorServices #1268](https://github.com/PowerShell/PowerShellEditorServices/pull/1268) -
  Refactor GetCommandHandler to not use dynamic.
- 🔍 🐛 [vscode-powershell #2654](https://github.com/PowerShell/PowerShellEditorServices/pull/1270) -
  Fix interpolation in Log points, switch to double quotes. (Thanks @rkeithhill!)
- [PowerShellEditorServices #1267](https://github.com/PowerShell/PowerShellEditorServices/pull/1267) -
  Update module manifest to match current module.
- 📟 🐛 [vscode-powershell #2637](https://github.com/PowerShell/PowerShellEditorServices/pull/1264) -
  Leverage internal HostUI to check if VT100 is supported.
- 📟 🐛 [vscode-powershell #2637](https://github.com/PowerShell/PowerShellEditorServices/pull/1263) -
  Use stable builds of PSReadLine for the PowerShell extension and preview builds for the PowerShell Preview extension.
- 💎 ✨ [vscode-powershell #2543](https://github.com/PowerShell/PowerShellEditorServices/pull/1262) -
  Allow formatting when ScriptAnalysis setting is set to disabled.

## v2.1.0
### Thursday, April 15, 2020
#### Notable features and fixes

- ⚡️🧠 Better performance of overall but especially IntelliSense.
- 🐛📟 Errors show up properly on screen in PowerShell Integrated Console.
- ✨🐢 Run a single test in Pester v5 by setting `"powershell.pester.useLegacyCodeLens": false`.
- 🐛🔧 Ignore files specified in `files.exclude` and `search.exclude` in reference/CodeLens search.

## v2.1.0-preview.6
### Monday, April 13, 2020

- 🐛📟 [PowerShellEditorServices #1258](https://github.com/PowerShell/PowerShellEditorServices/pull/1258) -
  No more warning about PowerShellEditorServices module being imported with unapproved verb.

## v2.1.0-preview.5
### Thursday, April 09, 2020

- ✨📟 [PowerShellEditorServices #1255](https://github.com/PowerShell/PowerShellEditorServices/pull/1255) -
  Move PSReadLine invocation into cmdlets to get closer to supporting ConstrainedLanguage mode. Also removes hard coded PSReadLine assembly version.

## v2.1.0-preview.4
### Wednesday, April 08, 2020

- ✨👷 [PowerShellEditorServices #1252](https://github.com/PowerShell/PowerShellEditorServices/pull/1252) -
  Use PowerShell Daily in CI.
- 🐛⚡️🧠🔗 [PowerShellEditorServices #1251](https://github.com/PowerShell/PowerShellEditorServices/pull/1251) -
  Add cancellation to SignatureHelp request and cache results for cmdlets on `Get-Command` and `Get-Help`.

## v2.1.0-preview.3
### Tuesday, March 31, 2020

- ✨📟 [PowerShellEditorServices #1245](https://github.com/PowerShell/PowerShellEditorServices/pull/1245) -
  Better PSReadLine version filter check to include 2.1.0+ prereleases.
- 🐛⚡️🧠🔗 [PowerShellEditorServices #1248](https://github.com/PowerShell/PowerShellEditorServices/pull/1248) -
  Fix cancellation for completions and add `textDocument/hover` cancellation support.

## v2.1.0-preview.2
### Thursday, March 26, 2020

- 🐛🧠 [vscode-powershell #2584](https://github.com/PowerShell/PowerShellEditorServices/pull/1243) -
  Refactor GetCommandSynopsisAsync method to make sure cmdlets with module prefixes work.
- 🐛⚡️🧠📚 [vscode-powershell #2556](https://github.com/PowerShell/PowerShellEditorServices/pull/1238) -
  Add cancellation for `textDocument/completion`, `textDocument/codeAction`, `textDocument/folding`.
- ✨👮 [vscode-powershell #2572](https://github.com/PowerShell/PowerShellEditorServices/pull/1241) -
  Only run diagnostics on PowerShell files.
- ⚡️🧠 [PowerShellEditorServices #1237](https://github.com/PowerShell/PowerShellEditorServices/pull/1237) -
  Optimize when we run GetCommandInfoAsync to use the pipeline less for Intellisense.

## v2.1.0-preview.1
### Thursday, March 12, 2020

- ✨🧠 [PowerShellEditorServices #1232](https://github.com/PowerShell/PowerShellEditorServices/pull/1232) -
  Only resolve completion items from commands.
- ✨🐢 [PowerShellEditorServices #1167](https://github.com/PowerShell/PowerShellEditorServices/pull/1167) -
  Run a single test in Pester v5. (Thanks @nohwnd!)
- 🐛🔍 [vscode-powershell #2534](https://github.com/PowerShell/PowerShellEditorServices/pull/1230) -
  Ensure that errors are written to the console when debugging.
- 🐛🔍 [vscode-powershell #2525](https://github.com/PowerShell/PowerShellEditorServices/pull/1229) -
  Don't warn users when using `Clear-Host` in temp sessions.
- ✨💎 [PowerShellEditorServices #1228](https://github.com/PowerShell/PowerShellEditorServices/pull/1228) -
  Add better logging for formatter and refactor it into 1 class.
- 🐛🚂 [vscode-powershell #2397](https://github.com/PowerShell/PowerShellEditorServices/pull/1227) -
  Use Assembly.LoadFile for dependency loading in WinPS.
- ✨🛫 [PowerShellEditorServices #1222](https://github.com/PowerShell/PowerShellEditorServices/pull/1222) -
  Make initial logging work in constrained language mode, allowing the desired user-facing error to present.
- 🐛🛫 [PowerShellEditorServices #1225](https://github.com/PowerShell/PowerShellEditorServices/pull/1225) -
  Sign Clear-Host.ps1.
- 🐛🛫 [PowerShellEditorServices #1219](https://github.com/PowerShell/PowerShellEditorServices/pull/1219) -
  Ensure log directory is created.
- 🐛👷‍♀️ [PowerShellEditorServices #1223](https://github.com/PowerShell/PowerShellEditorServices/pull/1223) -
  Change Ms-vscode.csharp to ms-dotnettools.csharp. (Thanks @devlead!)
- 🐛🔧 [PowerShellEditorServices #1220](https://github.com/PowerShell/PowerShellEditorServices/pull/1220) -
  Fix typo in settings.
- ✨🔧 [PowerShellEditorServices #1218](https://github.com/PowerShell/PowerShellEditorServices/pull/1218) -
  Switch to better document selecting for vim extension.
- 🐛🧠 [PowerShellEditorServices #1217](https://github.com/PowerShell/PowerShellEditorServices/pull/1217) -
  Make session-state lock task-reentrant to fix Untitled file debugging.

## v2.0.0
### Thursday, March 5, 2020

- 🐛📟 [PowerShellEditorServices #1201](https://github.com/PowerShell/PowerShellEditorServices/pull/1201) -
  Fix newlines in error formatting.
- 🐛👮 [vscode-PowerShell #2489](https://github.com/PowerShell/PowerShellEditorServices/pull/1206) -
  Fix PSScriptAnalyzer not using default rules when no settings file present.
- 🐛📟 [vscode-PowerShell #2291](https://github.com/PowerShell/PowerShellEditorServices/pull/1207) -
  Fix `Read-Host` dropping characters.
- 🐛📺 [vscode-PowerShell #2424](https://github.com/PowerShell/PowerShellEditorServices/pull/1209) -
  Fix `F8` not working repeatedly in an Interactive Debugging session.
- 🐛🛫 [vscode-PowerShell #2404](https://github.com/PowerShell/PowerShellEditorServices/pull/1208) -
  Fix execution policy being set incorrectly at startup on Windows.
- 🐛🧠 [vscode-PowerShell #2364](https://github.com/PowerShell/PowerShellEditorServices/pull/1210) -
  Fix intellisense and `F5` not working after debugging.
- 🐛🧰 [vscode-PowerShell #2495](https://github.com/PowerShell/PowerShellEditorServices/pull/1211) -
  Fix PowerShellEditorServices.Commands module commands not working due to types being moved.
- 🐛👮 [vscode-PowerShell #2516](https://github.com/PowerShell/PowerShellEditorServices/pull/1216) -
  Fix CommentHelp for when a function has other problems with it.

## v2.0.0-preview.9
### Thursday, February 20, 2020

- 🐛📁 [vscode-PowerShell #2421](https://github.com/powershell/powershelleditorservices/pull/1161) -
  Fix WorkspacePath so that references work with non-ASCII characters.
- 🐛📟 [vscode-PowerShell #2372](https://github.com/powershell/powershelleditorservices/pull/1162) -
  Fix prompt behavior when debugging.
- 🐛🛫 [PowerShellEditorServices #1171](https://github.com/powershell/powershelleditorservices/pull/1171) -
  Fix race condition where running multiple profiles caused errors.
- 🐛📟 [vscode-PowerShell #2420](https://github.com/powershell/powershelleditorservices/pull/1173) -
  Fix an issue where pasting to a `Get-Credential` prompt in some Windows versions caused a crash.
- 🐛📟 [vscode-PowerShell #1790](https://github.com/powershell/powershelleditorservices/pull/1174) -
  Fix an inconsistency where `Read-Host -Prompt 'prompt'` would return `$null` rather than empty string
  when given no input.
- 🐛🔗 [PowerShellEditorServices #1177](https://github.com/powershell/powershelleditorservices/pull/1174) -
  Fix an issue where untitled files did not work with CodeLens.
- ⚡️⏱️ [PowerShellEditorServices #1172](https://github.com/powershell/powershelleditorservices/pull/1172) -
  Improve `async`/`await` and `Task` usage to reduce concurrency overhead and improve performance.
- 🐛📟 [PowerShellEditorServices #1178](https://github.com/powershell/powershelleditorservices/pull/1178) -
  Improve PSReadLine experience where no new line is rendered in the console.
- ✨🔍 [PowerShellEditorServices #1119](https://github.com/powershell/powershelleditorservices/pull/1119) -
  Enable new debugging APIs added in PowerShell 7, improving performance and fixing issues where
  the debugger would stop responding or be unable to update breakpoints while scripts were running.
- 👷📟 [PowerShellEditorServices #1187](https://github.com/PowerShell/PowerShellEditorServices/pull/1187) -
  Upgrade built-in PSReadLine to 2.0.0 GA.
- 🐛👮 [PowerShellEditorServices #1179](https://github.com/PowerShell/PowerShellEditorServices/pull/1179) -
  Improve integration with PSScriptAnalyzer, improving performance,
  fixing an error when PSScriptAnalyzer is not available, fix CodeActions not appearing on Windows,
  fix an issue where the PSModulePath is reset by PSScriptAnalyzer opening new runspaces.
- 🚂 [PowerShellEditorServices #1183](https://github.com/PowerShell/PowerShellEditorServices/pull/1183) -
  Close over public APIs not intended for external use and replace with new, async-friendly APIs.

## v2.0.0-preview.8
### Monday, January 13, 2020

- 📺 [vscode-powershell #2405](https://github.com/PowerShell/PowerShellEditorServices/pull/1152) -
  Add tooltip to completions ParameterValue.
- 🛫 🐛 [vscode-powershell #2393](https://github.com/PowerShell/PowerShellEditorServices/pull/1151) -
  Probe netfx dir for deps.
- 🚂 ⏱️ 🐛 [vscode-powershell #2352](https://github.com/PowerShell/PowerShellEditorServices/pull/1149) -
  Fix lock up that occurs when WinForms is executed on the pipeline thread.
- 💭 🐛 [vscode-powershell #2402](https://github.com/PowerShell/PowerShellEditorServices/pull/1150) -
  Fix temp debugging after it broke bringing in $psEditor.
- 🧠 🐛 [vscode-powershell #2324](https://github.com/PowerShell/PowerShellEditorServices/pull/1143) -
  Fix unicode character uri bug.
- 🛫 📺 ✨ [vscode-powershell #2370](https://github.com/PowerShell/PowerShellEditorServices/pull/1141) -
  Make startup banner simpler.
- [vscode-powershell #2386](https://github.com/PowerShell/PowerShellEditorServices/pull/1140) -
  Fix uncaught exception when SafeToString returns null. (Thanks @jborean93!)
- 🔗 🐛 [vscode-powershell #2374](https://github.com/PowerShell/PowerShellEditorServices/pull/1139) -
  Simplify logic of determining Reference definition.
- 🛫 🐛 [vscode-powershell #2379](https://github.com/PowerShell/PowerShellEditorServices/pull/1138) -
  Use -Option AllScope to fix Windows PowerShell error.
- 👷 [PowerShellEditorServices #1158](https://github.com/PowerShell/PowerShellEditorServices/pull/1158) -
 Sets the distribution channel env var to "PSES" so starts can be distinguished in PS7+ telemetry

## v2.0.0-preview.7
### Wednesday, December 11, 2019

- 👷 📟 [PowerShellEditorServices #1129](https://github.com/PowerShell/PowerShellEditorServices/pull/1129) -
  Update PSReadLine to 2.0.0-rc1 in modules.json.
- 🛫 🐛 ⚡️ [vscode-powershell #2292](https://github.com/PowerShell/PowerShellEditorServices/pull/1118) -
  Isolate PSES dependencies from PowerShell on load + make PSES a pure binary module.
- ✨ 📟 [PowerShellEditorServices #1108](https://github.com/PowerShell/PowerShellEditorServices/pull/1108) -
  Clear the terminal via the LSP.
- 🔍 🐛 [vscode-powershell #2319](https://github.com/PowerShell/PowerShellEditorServices/pull/1117) -
  Run one invocation per SetBreakpoints request. (Thanks @SeeminglyScience!)
- 🐛 [PowerShellEditorServices #1114](https://github.com/PowerShell/PowerShellEditorServices/pull/1114) -
  Fix Import-EditorCommand -Module. (Thanks @sk82jack!)
- 🐛 🔍 [PowerShellEditorServices #1112](https://github.com/PowerShell/PowerShellEditorServices/pull/1112) -
  Fix breakpoint setting deadlock.
- 🔗 🐛 [vscode-powershell #2306](https://github.com/PowerShell/PowerShellEditorServices/pull/1110) -
  Fix references on Windows due to bad WorkspacePath.
- ✨ 👷 [PowerShellEditorServices #993](https://github.com/PowerShell/PowerShellEditorServices/pull/993) -
  Add devcontainer support for building in container. (Thanks @bergmeister!)
- 🛫 🐛 [vscode-powershell #2311](https://github.com/PowerShell/PowerShellEditorServices/pull/1107) -
  Protect against no RootUri (no open workspace).
- 🐛 📟 [vscode-powershell #2274](https://github.com/PowerShell/PowerShellEditorServices/pull/1092) -
  Fix '@' appearing in console.
- 👮‍ 🐛 [vscode-powershell #2288](https://github.com/PowerShell/PowerShellEditorServices/pull/1094) -
  Use RootUri.LocalPath for workspace path.
- 🐛 👮‍ [PowerShellEditorServices #1101](https://github.com/PowerShell/PowerShellEditorServices/pull/1101) -
  Add `PSAvoidAssignmentToAutomaticVariable` to the default set of PSSA rules. (Thanks @bergmeister!)
- 👮‍ 🔗 🐛 [vscode-powershell #2290](https://github.com/PowerShell/PowerShellEditorServices/pull/1098) -
  Fix diagnostics not showing in untitled files and now also show CodeLens.
- 🔍 🐛 [vscode-powershell #1850](https://github.com/PowerShell/PowerShellEditorServices/pull/1097) -
  Fixes no prompt showing up when debugging.
- 🚂 📺 🐛 [vscode-powershell #2284](https://github.com/PowerShell/PowerShellEditorServices/pull/1096) -
  Fix running indicator by ignoring PSRL aborts.

## v2.0.0-preview.6
### Friday, November 1, 2019

#### Special Note
In this release of the preview extension,
we've merged significant architectural work into PowerShell Editor Services.
After several months of work, PSES now uses the Omnisharp LSP library
to handle Language Server Protocol interaction instead of rolling its own,
allowing PSES to concentrate on being a good PowerShell backend.
We hope you'll see increased performance and stability in this release.
As always, [please let us know if you find any issues](https://github.com/PowerShell/PowerShellEditorServices/issues/new).

- 🐛 [PowerShellEditorServices #1080](https://github.com/PowerShell/PowerShellEditorServices/pull/1080) -
  Remove extra newline in GetComment feature.
- 🐛 [PowerShellEditorServices #1079](https://github.com/PowerShell/PowerShellEditorServices/pull/1079) -
  Fix duplicate diagnostics caused by DidChange handler.
- 🔧 [PowerShellEditorServices #1076](https://github.com/PowerShell/PowerShellEditorServices/pull/1076) -
  Graduate PSReadLine feature and add UseLegacyReadLine.
- ⚙️ [PowerShellEditorServices #1075](https://github.com/PowerShell/PowerShellEditorServices/pull/1075) -
  Lock OmniSharp dependencies to v0.14.0. (Thanks @mholo65!)
- 📟 [PowerShellEditorServices #1064](https://github.com/PowerShell/PowerShellEditorServices/pull/1064) -
  Add support for terminal error color settings in PS7.
- 🐛 [PowerShellEditorServices #1073](https://github.com/PowerShell/PowerShellEditorServices/pull/1073) -
  Fix prerelease version discovery and fix omnisharp change.
- 🐛 [PowerShellEditorServices #1065](https://github.com/PowerShell/PowerShellEditorServices/pull/1065) -
  Fix TEMP debugging.
- 🐛 [vscode-powershell #1753](https://github.com/PowerShell/PowerShellEditorServices/pull/1072) -
  Override PSRL ReadKey on Windows as well.
- 💭 [PowerShellEditorServices #1066](https://github.com/PowerShell/PowerShellEditorServices/pull/1066) -
  Rework Omnisharp logging integration to make logging to files work again.
- 👷 [PowerShellEditorServices #1055](https://github.com/PowerShell/PowerShellEditorServices/pull/1055) -
  Update .Net Core SDK from 2.1.801 to 2.1.802 (latest patch). (Thanks @bergmeister!)
- 🚂 [PowerShellEditorServices #1056](https://github.com/PowerShell/PowerShellEditorServices/pull/1056) -
  Re-architect PowerShell Editor Services to use the Omnisharp LSP platform.
- 🐛 [vscode-powershell #2116](https://github.com/PowerShell/PowerShellEditorServices/pull/1044) -
  Fix UNC intellisense backslash.

## v2.0.0-preview.5
### Monday, September 23, 2019

- 🐛 [PowerShellEditorServices #1022](https://github.com/PowerShell/PowerShellEditorServices/pull/1022) -
  Catch stream exceptions for some Debug Adapter stability.
- 🔎 [PowerShellEditorServices #1021](https://github.com/PowerShell/PowerShellEditorServices/pull/1021) -
  Add AutoCorrectAliases setting (PR to be made in VS-Code repo as well) to add support for optionally correcting aliases as well (added in PSSA 1.18.2). (Thanks @bergmeister!).
- 🐛 [vscode-powershell #1994](https://github.com/PowerShell/PowerShellEditorServices/pull/1000) -
  Fix crash when setBreakpoint from VSCode sends a git:/ URI.
- 🧹 [PowerShellEditorServices #988](https://github.com/PowerShell/PowerShellEditorServices/pull/988) -
  Remove consoleecho lib for PowerShell 7.
- 📔 [PowerShellEditorServices #986](https://github.com/PowerShell/PowerShellEditorServices) -
  Documentation updates. (Thanks @SydneyhSmith!)
- ⚙️ [PowerShellEditorServices #981](https://github.com/PowerShell/PowerShellEditorServices/pull/981) -
  Update NewtonSoft.Json dependency from 10.0.3 to 11.02 since PS 6.0 has been deprecated. (Thanks @bergmeister!)
- 🐛 [vscode-powershell #2007](https://github.com/PowerShell/PowerShellEditorServices/pull/974) -
  Defend against crash when no PSScriptAnalyzer is found.
- 👷 [PowerShellEditorServices #978](https://github.com/PowerShell/PowerShellEditorServices/pull/977) -
  Delete stale WebSocket code.

## v2.0.0-preview.4
### Wednesday, May 22, 2019

- ✨ [PowerShellEditorServices #951](https://github.com/PowerShell/PowerShellEditorServices/pull/951) -
  Allow passing RunspaceName
- 🚨 [PowerShellEditorServices #944](https://github.com/PowerShell/PowerShellEditorServices/pull/944) -
  Add integration testing module with simple tests to verify PSES starts and stops
- 🐛 [PowerShellEditorServices #954](https://github.com/PowerShell/PowerShellEditorServices/pull/955) -
  Ensure NamedPipeServerStream is assigned in Windows PowerShell
- ✨ [PowerShellEditorServices #952](https://github.com/PowerShell/PowerShellEditorServices/pull/952) -
  Update to PSReadLine 2.0.0-beta4
- ✨ [PowerShellEditorServices #877](https://github.com/PowerShell/PowerShellEditorServices/pull/877) -
  Add filtering for CodeLens and References (Thanks @glennsarti!)
- 🐛 [vscode-powershell #1933](https://github.com/PowerShell/PowerShellEditorServices/pull/949) -
  Stop crash when workspace doesn't exist
- 👷 [PowerShellEditorServices #878](https://github.com/PowerShell/PowerShellEditorServices/pull/878) -
  Remove native named pipes implementation
- 🐛 [PowerShellEditorServices #947](https://github.com/PowerShell/PowerShellEditorServices/pull/947) -
  Fix silent failure in VSCode WebViews by using Id for dictionary since multiple pages could have the same title
- 🐛 [PowerShellEditorServices #946](https://github.com/PowerShell/PowerShellEditorServices/pull/946) -
  Rename to use async
- 👷 [PowerShellEditorServices #943](https://github.com/PowerShell/PowerShellEditorServices/pull/943) -
  Improvements to the log parsing module (Thanks @rkeithhill!)
- 💻 [PowerShellEditorServices #921](https://github.com/PowerShell/PowerShellEditorServices/pull/921) -
  Set up CI with Azure Pipelines
- 🐛 [PowerShellEditorServices #908](https://github.com/PowerShell/PowerShellEditorServices/pull/908) -
  Fix issue with reference code lens not working with UNC paths (Thanks @rkeithhill!)
- 🐛 [vscode-powershell #1571](https://github.com/PowerShell/PowerShellEditorServices/pull/911) -
  Fix faulty netfx check
- 🐛 [PowerShellEditorServices #906](https://github.com/PowerShell/PowerShellEditorServices/pull/906) -
  Fix New-EditorFile with no folder or no files open
- ✨ [vscode-powershell #1398](https://github.com/PowerShell/PowerShellEditorServices/pull/902) -
  Improve path auto-completion (Thanks @rkeithhill!)
- 🐛 [PowerShellEditorServices #910](https://github.com/PowerShell/PowerShellEditorServices/pull/910) -
  Fix UseCorrectCasing to be actually configurable via `powershell.codeFormatting.useCorrectCasing` (Thanks @bergmeister!)
- 👷 [PowerShellEditorServices #909](https://github.com/PowerShell/PowerShellEditorServices/pull/909) -
  Use global.json to pin .Net Core SDK version and update it from 2.1.402 to 2.1.602 (Thanks @bergmeister!)
- 👷 [PowerShellEditorServices #903](https://github.com/PowerShell/PowerShellEditorServices/pull/903) -
  Move temp folder into repo to avoid state that causes build errors from time to time when rebuilding locally (and packages have updated) (Thanks @bergmeister!)
- 💻 [PowerShellEditorServices #904](https://github.com/PowerShell/PowerShellEditorServices/pull/904) -
  Add initial credscan configuation ymls for CI
- 🐛 [PowerShellEditorServices #901](https://github.com/PowerShell/PowerShellEditorServices/pull/901) -
  Switch to current lowercase names for powershell and mdlint exts (Thanks @rkeithhill!)

## v2.0.0-preview.3
### Wednesday, April 10, 2019

- [PowerShellEditorServices #906](https://github.com/PowerShell/PowerShellEditorServices/pull/906) -
  Fix New-EditorFile with no folder or no files open
- [PowerShellEditorServices #908](https://github.com/PowerShell/PowerShellEditorServices/pull/908) -
  Fix crash in CodeLens with UNC paths on Windows (Thanks @rkeithhill!)
- [PowerShellEditorServices #902](https://github.com/PowerShell/PowerShellEditorServices/pull/902) -
  Improve path auto-completion (Thanks @rkeithhill!)
- [PowerShellEditorServices #910](https://github.com/PowerShell/PowerShellEditorServices/pull/910) -
  Fix UseCorrectCasing to be actually configurable via `powershell.codeFormatting.useCorrectCasing` (Thanks @bergmeister!)
- [PowerShellEditorServices #909](https://github.com/PowerShell/PowerShellEditorServices/pull/909) -
  Use global.json to pin .Net Core SDK version and update it from 2.1.402 to 2.1.602 (Thanks @bergmeister!)
- [PowerShellEditorServices #903](https://github.com/PowerShell/PowerShellEditorServices/pull/903) -
  Move temp folder into repo to avoid state that causes build errors from time to time when rebuilding locally (and packages have updated) (Thanks @bergmeister!)

## v2.0.0-preview.2
### Friday, March 29, 2019

- [PowerShellEditorServices #895](https://github.com/PowerShell/PowerShellEditorServices/pull/895) -
  Add warning to parameter validation set  (Thanks @Benny1007!)
- [PowerShellEditorServices #897](https://github.com/PowerShell/PowerShellEditorServices/pull/897) -
  Clean up and pop dead runspaces when using 'attach' debugging
- [PowerShellEditorServices #888](https://github.com/PowerShell/PowerShellEditorServices/pull/888) -
  Add new ParseError level to ScriptFileMarkerLevel and filter out PSSA parse errors
- [PowerShellEditorServices #858](https://github.com/PowerShell/PowerShellEditorServices/pull/858) -
  Fix XUnit warnings that better assertion operators should be used. (Thanks @bergmeister!)
- [PowerShellEditorServices #854](https://github.com/PowerShell/PowerShellEditorServices/pull/854) -
  Reinstate test filtering (Thanks @glennsarti!)
- [PowerShellEditorServices #866](https://github.com/PowerShell/PowerShellEditorServices/pull/866) -
  Catch NotSupportedException which can be thrown by FileStream constructor (Thanks @rkeithhill!)
- [PowerShellEditorServices #868](https://github.com/PowerShell/PowerShellEditorServices/pull/868) -
  Speed up Travis builds by skipping the .NET Core initialization (Thanks @bergmeister!)
- [PowerShellEditorServices #869](https://github.com/PowerShell/PowerShellEditorServices/pull/869) -
  Added `AsNewFile` switch to Out-CurrentFile (Thanks @dfinke!)
- [PowerShellEditorServices #873](https://github.com/PowerShell/PowerShellEditorServices/pull/873) -
  Return the start line number for Describe block (Thanks @rkeithhill!)
- [PowerShellEditorServices #876](https://github.com/PowerShell/PowerShellEditorServices/pull/876) -
  Temporarily disable deemphasized stack frames to fix VSCode issue 1750 (Thanks @rkeithhill!)
- [PowerShellEditorServices #871](https://github.com/PowerShell/PowerShellEditorServices/pull/871) -
  Support -CustomPipeName, allowing configuration of custom namedpipes for LSP transport
- [PowerShellEditorServices #872](https://github.com/PowerShell/PowerShellEditorServices/pull/872) -
  Fix unable to open files in problems/peek windows issue (Thanks @rkeithhill!)
- [PowerShellEditorServices #875](https://github.com/PowerShell/PowerShellEditorServices/pull/875) -
  Add attach to local runspace. (Thanks @adamdriscoll!)
- [PowerShellEditorServices #881](https://github.com/PowerShell/PowerShellEditorServices/pull/881) -
  Use `NamedPipeConnectionInfo` to connect to remote runspaces instead of Enter-PSHostProcess
- [PowerShellEditorServices #845](https://github.com/PowerShell/PowerShellEditorServices/pull/845) -
  Enable UseCorrectCasing as a default rule (Thanks @bergmeister!)
- [PowerShellEditorServices #835](https://github.com/PowerShell/PowerShellEditorServices/pull/835) -
  Map new `powershell.codeformatting` settings WhitespaceInsideBrace and WhitespaceAroundPipe to PSSA settings hashtable (Thanks @bergmeister!)
- [PowerShellEditorServices #836](https://github.com/PowerShell/PowerShellEditorServices/pull/836) -
  Add PipelineIndentationStyle configuration mapping (Thanks @bergmeister!)
- [PowerShellEditorServices #887](https://github.com/PowerShell/PowerShellEditorServices/pull/887) -
  Cherry pick PR 1750 merge commit to legacy/v1.x, has additional fixes (Thanks @rkeithhill!)
- [PowerShellEditorServices #874](https://github.com/PowerShell/PowerShellEditorServices/pull/874) -
  Use public `InternalHost` from origin runspace (Thanks @SeeminglyScience!)
- [PowerShellEditorServices #889](https://github.com/PowerShell/PowerShellEditorServices/pull/889) -
  Enhance Get-PsesRpcNotificationMessage/MessageResponseTimes to allow filtering by message name (Thanks @rkeithhill!)
- [PowerShellEditorServices #859](https://github.com/PowerShell/PowerShellEditorServices/pull/859) -
  Upgrade PowerShellStandard.Library, PowerShell.SDK, NET.Test.SDK and Serilog NuGet packages to latest released version and enable AppVeyor build on any branch (Thanks @bergmeister!)
- [PowerShellEditorServices #862](https://github.com/PowerShell/PowerShellEditorServices/pull/862) -
  Handle arbitrary exceptions when recursing workspace

## v2.0.0-preview.1
### Wednesday, January 23, 2019

#### Preview builds of PowerShell Editor Services are now available

#### What the first preview contains

The v2.0.0-preview.1 version of the extension is built on .NET Standard (enabling support for both Windows PowerShell and PowerShell Core from one assembly)

It also contains PSReadLine support in the integrated console for Windows behind a feature flag. PSReadLine provides a consistent and rich interactive experience, including syntax coloring and multi-line editing and history, in the PowerShell console, in Cloud Shell, and now in VSCode terminal. For more information on the benefits of PSReadLine, check out their [documentation](https://docs.microsoft.com/en-us/powershell/module/psreadline/about/about_psreadline?view=powershell-6).

To enable PSReadLine support in the Preview version on Windows, please add the following flag to your `Start-EditorServices.ps1` call:

```
-FeatureFlags @('PSReadLine')
```

HUGE thanks to @SeeminglyScience for all his amazing work getting PSReadLine working in PowerShell Editor Services!

#### Breaking Changes

Due to the above changes, this version of the PowerShell Editor Services only works with Windows PowerShell 5.1 and PowerShell Core 6.

- [PowerShellEditorServices #792](https://github.com/PowerShell/PowerShellEditorServices/pull/792) -
  Add Async suffix to async methods (Thanks @dee-see!)
- [PowerShellEditorServices #775](https://github.com/PowerShell/PowerShellEditorServices/pull/775) -
  Removed ShowOnlineHelp Message (Thanks @corbob!)
- [PowerShellEditorServices #769](https://github.com/PowerShell/PowerShellEditorServices/pull/769) -
  Set Runspaces to use STA when running in Windows PowerShell
- [PowerShellEditorServices #741](https://github.com/PowerShell/PowerShellEditorServices/pull/741) -
  Migrate to netstandard2.0 and PSStandard
- [PowerShellEditorServices #672](https://github.com/PowerShell/PowerShellEditorServices/pull/672) -
  PSReadLine integration (Thanks @SeeminglyScience!)

## v1.10.2
### Tuesday, December 18, 2018

- [PowerShellEditorServices #811](https://github.com/PowerShell/PowerShellEditorServices/pull/805) -
  Fix token-based folding (thanks @glennsarti!)
- [PowerShellEditorServices #823](https://github.com/PowerShell/PowerShellEditorServices/pull/823) -
  Fix case-sensitivity of Pester CodeLens (thanks @bergmeister!)
- [PowerShellEditorServices #815](https://github.com/PowerShell/PowerShellEditorServices/pull/815) -
  Fix crash when untitled files opened as PowerShell
- [PowerShellEditorServices #826](https://github.com/PowerShell/PowerShellEditorServices/pull/826) -
  Fix crash when duplicate references are present in the same file

## v1.10.1
### Friday, December 7, 2018

- [PowerShellEditorServices #808](https://github.com/PowerShell/PowerShellEditorServices/pull/808) -
  Fix startup crash on Windows 7
- [PowerShellEditorServices #807](https://github.com/PowerShell/PowerShellEditorServices/pull/807) -
  Fix deadlock occurring while connecting to named pipes

## v1.10.0
### Monday, December 3, 2018

- [PowerShellEditorServices #786](https://github.com/PowerShell/PowerShellEditorServices/pull/786) -
  Fix #17: Add go to definition support for dot sourced file paths  (Thanks @dee-see!)
- [PowerShellEditorServices #767](https://github.com/PowerShell/PowerShellEditorServices/pull/767) -
  Change unhandled messages to warnings instead of errors
- [PowerShellEditorServices #765](https://github.com/PowerShell/PowerShellEditorServices/pull/765) -
  Fix PowerShell wildcard escaping in debug paths
- [PowerShellEditorServices #778](https://github.com/PowerShell/PowerShellEditorServices/pull/778) -
  Fix multiple occurrences of the same typo  (Thanks @dee-see!)
- [PowerShellEditorServices #782](https://github.com/PowerShell/PowerShellEditorServices/pull/782) -
  Fix #779: NRE on Dispose in ExecutionTimer  (Thanks @dee-see!)
- [PowerShellEditorServices #772](https://github.com/PowerShell/PowerShellEditorServices/pull/772) -
  Add build information to releases to document it in logs
- [PowerShellEditorServices #774](https://github.com/PowerShell/PowerShellEditorServices/pull/774) -
  New-EditorFile works on non-powershell untitled files
- [PowerShellEditorServices #787](https://github.com/PowerShell/PowerShellEditorServices/pull/787) -
  Fix descion/decision typo in visitors  (Thanks @dee-see!)
- [PowerShellEditorServices #784](https://github.com/PowerShell/PowerShellEditorServices/pull/784) -
  Replace bad StringReader usage with String.Split()
- [PowerShellEditorServices #768](https://github.com/PowerShell/PowerShellEditorServices/pull/768) -
  Make pipeline runtime exceptions warnings in log
- [PowerShellEditorServices #790](https://github.com/PowerShell/PowerShellEditorServices/pull/790) -
  Add managed thread id to log output to add debugging threading issues  (Thanks @rkeithhill!)
- [PowerShellEditorServices #794](https://github.com/PowerShell/PowerShellEditorServices/pull/794) -
  Fix Pester CodeLens run/debug by not quoting params/already quoted args  (Thanks @rkeithhill!)
- [PowerShellEditorServices #785](https://github.com/PowerShell/PowerShellEditorServices/pull/785) -
  Adds ability to use separate pipes for reading and writing  (Thanks @ant-druha!)
- [PowerShellEditorServices #796](https://github.com/PowerShell/PowerShellEditorServices/pull/796) -
  Code cleanup of the  start script and ESHost.cs file  (Thanks @rkeithhill!)
- [PowerShellEditorServices #795](https://github.com/PowerShell/PowerShellEditorServices/pull/795) -
  Fix file recursion overflow problems when enumerating directories in workspaces
- [PowerShellEditorServices #697](https://github.com/PowerShell/PowerShellEditorServices/pull/697) -
  Add functionality to allow a Show-Command-like panel in VS Code  (Thanks @corbob!)
- [PowerShellEditorServices #777](https://github.com/PowerShell/PowerShellEditorServices/pull/777) -
  Move syntax folding processing to language server (Thanks @glennsarti!)
- [PowerShellEditorServices #801](https://github.com/PowerShell/PowerShellEditorServices/pull/801) -
  Fix remoting to local PowerShell instances (e.g. WSL)
- [PowerShellEditorServices #797](https://github.com/PowerShell/PowerShellEditorServices/pull/797) -
  Start of a PSES log file analyzer  (Thanks @rkeithhill!)
- [PowerShellEditorServices #789](https://github.com/PowerShell/PowerShellEditorServices/pull/789) -
  Add support for a "Show Documentation" quick fix menu entry  (Thanks @rkeithhill!)
- [PowerShellEditorServices #760](https://github.com/PowerShell/PowerShellEditorServices/pull/760) -
  Fix exception when remoting from Windows to non-Windows (Thanks @SeeminglyScience!)

## v1.9.0
### Thursday, September 27, 2018

- [PowerShellEditorServices #750](https://github.com/PowerShell/PowerShellEditorServices/pull/750) -
  Fix issue where # in path causes the path to resolve incorrectly
- [PowerShellEditorServices #721](https://github.com/PowerShell/PowerShellEditorServices/pull/721) -
  Change Get-Help behavior to return local help when online help can't be displayed  (Thanks @corbob!)
- [PowerShellEditorServices #748](https://github.com/PowerShell/PowerShellEditorServices/pull/748) -
  Fix index out-of-range exception when deleting script files
- [PowerShellEditorServices #749](https://github.com/PowerShell/PowerShellEditorServices/pull/749) -
  Fix crash for finding symbols on bad paths
- [PowerShellEditorServices #740](https://github.com/PowerShell/PowerShellEditorServices/pull/740) -
  Fix inner help completion
- [PowerShellEditorServices #736](https://github.com/PowerShell/PowerShellEditorServices/pull/736) -
  Cache the reflection call done for completions
- [PowerShellEditorServices #737](https://github.com/PowerShell/PowerShellEditorServices/pull/737) -
  Remove LINQ usage in language service methods
- [PowerShellEditorServices #743](https://github.com/PowerShell/PowerShellEditorServices/pull/743) -
  Remove unnecessary LINQ calls from LanguageServer

## v1.8.4
### Friday, August 31, 2018

- [PowerShellEditorServices #728](https://github.com/PowerShell/PowerShellEditorServices/pulls/728) -
  Fix formatter crash when script contains parse errors
- [PowerShellEditorServices #730](https://github.com/PowerShell/PowerShellEditorServices/pulls/730) -
  Fix crash where lines appended to end of script file causes out of bounds exception
- [PowerShellEditorServices #732](https://github.com/PowerShell/PowerShellEditorServices/pulls/732) -
  Fix CodeLens crash when a file cannot be opened, stop unnecessary file reads in CodeLens
- [PowerShellEditorServices #729](https://github.com/PowerShell/PowerShellEditorServices/pulls/729) -
  Fix a null dereference when an invalid cast exception has no inner exception
- [PowerShellEditorServices #719](https://github.com/PowerShell/PowerShellEditorServices/pulls/719) -
  Reduce allocations in the CodeLens providers
- [PowerShellEditorServices #725](https://github.com/PowerShell/PowerShellEditorServices/pulls/725) -
  Fix null dereference when debugging untitlted filesj
- [PowerShellEditorServices #726](https://github.com/PowerShell/PowerShellEditorServices/pulls/726) -
  Fix comment-based help snippet

## v1.8.3
### Wednesday, August 15, 2018

#### Fixes and Improvements

- [PowerShell/PowerShellEditorServices #722](https://github.com/PowerShell/PowerShellEditorServices/pull/722) -
  Add VSTS signing step
- [PowerShell/PowerShellEditorServices #717](https://github.com/PowerShell/PowerShellEditorServices/pull/717) -
  Increment version for prerelease
- [PowerShell/PowerShellEditorServices #715](https://github.com/PowerShell/PowerShellEditorServices/pull/715) -
  Reduce allocations when parsing files (Thanks @mattpwhite!)

## v1.8.2
### Thursday, July 26, 2018

#### Fixes and Improvements

- [PowerShell/PowerShellEditorServices #712](https://github.com/PowerShell/PowerShellEditorServices/pull/712) -
  workaround to support inmemory:// (#712)
- [PowerShell/PowerShellEditorServices #706](https://github.com/PowerShell/PowerShellEditorServices/pull/706) -
  Go To Definition works with different Ast types
- [PowerShell/PowerShellEditorServices #707](https://github.com/PowerShell/PowerShellEditorServices/pull/707) -
  fix stdio passing
- [PowerShell/PowerShellEditorServices #709](https://github.com/PowerShell/PowerShellEditorServices/pull/709) -
  Stop Diagnostic logging from logging to stdio when the communication protocol is set to stdio
- [PowerShell/PowerShellEditorServices #710](https://github.com/PowerShell/PowerShellEditorServices/pull/710) -
  stdio should only launch language service not debug
- [PowerShell/PowerShellEditorServices #705](https://github.com/PowerShell/PowerShellEditorServices/pull/705) -
  Fix load order of PSSA modules
- [PowerShell/PowerShellEditorServices #704](https://github.com/PowerShell/PowerShellEditorServices/pull/704) -
  Do not enable PSAvoidTrailingWhitespace rule by default as it currenly flags whitespace-only lines as well (Thanks @bergmeister!)

## v1.8.1
### Wednesday, July 11, 2018

#### Fixes and Improvements

- [PowerShell/PowerShellEditorServices #699](https://github.com/PowerShell/PowerShellEditorServices/pull/699) -
  Replace `New-Guid` with `[guid]::NewGuid()` in startup script for PowerShell v3/4 compatibility

- [PowerShell/PowerShellEditorServices #698](https://github.com/PowerShell/PowerShellEditorServices/pull/698) -
  Fix usage of `stat` on Linux in startup script

## v1.8.0
### Tuesday, July 10, 2018

#### Fixes and Improvements

- [PowerShell/PowerShellEditorServices](https://github.com/PowerShell/PowerShellEditorServices/) -
  (Breaking Change) Remove TCP as a transport and secure named-pipe usage

- [Powershell/PowerShellEditorServices #696](https://github.com/PowerShell/PowerShellEditorServices/pull/696) -
  Add RenameProvider capability (Thanks @adamdriscoll!)

- [Powershell/PowerShellEditorServices #667](https://github.com/PowerShell/PowerShellEditorServices/pull/667) -
  Add .gitattributes, .editorconfig and extensions.json (Thanks @rkeithhill!)

- [Powershell/PowerShellEditorServices #668](https://github.com/PowerShell/PowerShellEditorServices/pull/668) -
  Stop the debugger service before we restart it

- [Powershell/PowerShellEditorServices #666](https://github.com/PowerShell/PowerShellEditorServices/pull/666) -
  Change logging to use Serilog

- [Powershell/PowerShellEditorServices #674](https://github.com/PowerShell/PowerShellEditorServices/pull/674) -
  Implement initialized notification handler to get rid of log error (Thanks @rkeithhill!)

- [Powershell/PowerShellEditorServices #675](https://github.com/PowerShell/PowerShellEditorServices/pull/675) -
  Add symbols to modules built in Debug configuration

- [Powershell/PowerShellEditorServices #669](https://github.com/PowerShell/PowerShellEditorServices/pull/669) -
  Add more useful PSSA rules that should be enabled by default (Thanks @bergmeister!)

- [Powershell/PowerShellEditorServices #681](https://github.com/PowerShell/PowerShellEditorServices/pull/681) -
  Initial CODEOWNERS file to auto assign PR reviewers (Thanks @rkeithhill!)

- [Powershell/PowerShellEditorServices](https://github.com/PowerShell/PowerShellEditorServices/) -
  Include ThirdPartyNotices.txt

- [Powershell/PowerShellEditorServices #685](https://github.com/PowerShell/PowerShellEditorServices/pull/685) -
  Fix PSES crash that happens if you format an empty PS doc (Thanks @rkeithhill!)

- [Powershell/PowerShellEditorServices #677](https://github.com/PowerShell/PowerShellEditorServices/pull/677) -
  Make AnalysisService use the latest version of PSScriptAnalyzer

- [Powershell/PowerShellEditorServices #686](https://github.com/PowerShell/PowerShellEditorServices/pull/686) -
  Fix issue where MS Dynamics CRM (Thanks @rkeithhill!)

- [Powershell/PowerShellEditorServices #687](https://github.com/PowerShell/PowerShellEditorServices/pull/687) -
  Add what to do when there's a vulnerability to docs

- [Powershell/PowerShellEditorServices #693](https://github.com/PowerShell/PowerShellEditorServices/pull/693) -
  Set DocumentRangeFormattingProvider value to false. (Thanks @adamdriscoll!)

- [Powershell/PowerShellEditorServices #691](https://github.com/PowerShell/PowerShellEditorServices/pull/691) -
  Fix error w/Start-EditorServices transcript logging using temp console (Thanks @rkeithhill!)

- [Powershell/PowerShellEditorServices #694](https://github.com/PowerShell/PowerShellEditorServices/pull/694) -
  Change debug launch handler to treat null/empty cwd to not change dir (Thanks @rkeithhill!)

- [Powershell/PowerShellEditorServices #664](https://github.com/PowerShell/PowerShellEditorServices/pull/664) -
  Ignore .idea folder that jetbrains products like to spit out (Rider, IntelliJ, Resharper) (#664)

- [Powershell/PowerShellEditorServices #663](https://github.com/PowerShell/PowerShellEditorServices/pull/663) -
  Close stray processes on exit (#663)

## 1.7.0
### Wednesday, April 25, 2018

#### Fixes and Improvements

- [PowerShell/PowerShellEditorServices #629](https://github.com/PowerShell/PowerShellEditorServices/pull/629) -
  Allow Tcp/NamedPipe/Stdio listeners to enable other editors to use PowerShell Editor Services. Thanks to [yatli](https://github.com/yatli) 🎉

- [PowerShell/PowerShellEditorServices #632](https://github.com/PowerShell/PowerShellEditorServices/pull/632) -
  Add events for PowerShell execution status (running, completed, etc).

- [PowerShell/PowerShellEditorServices #638](https://github.com/PowerShell/PowerShellEditorServices/pull/638) -
  Refactor pester script detection.

- [PowerShell/PowerShellEditorServices #639](https://github.com/PowerShell/PowerShellEditorServices/pull/639) -
  Add Start-EditorServices script from vscode-powershell repo.

- [PowerShell/PowerShellEditorServices #641](https://github.com/PowerShell/PowerShellEditorServices/pull/641) -
  Fix GetVersionDetails error on non-Windows platforms.

- [PowerShell/PowerShellEditorServices #642](https://github.com/PowerShell/PowerShellEditorServices/pull/642) -
  Add support for running xUnit tests in VS Test Explorer.

- [PowerShell/PowerShellEditorServices #643](https://github.com/PowerShell/PowerShellEditorServices/pull/643/files) -
  Fix issue using pre-release version of NET Core SDK.

- [PowerShell/PowerShellEditorServices #645](https://github.com/PowerShell/PowerShellEditorServices/pull/645) -
  Implemented a better way to test for in-memory file.

- [PowerShell/PowerShellEditorServices #647](https://github.com/PowerShell/PowerShellEditorServices/pull/647) -
  Change PSES to be buildable as a standalone.

- [PowerShell/PowerShellEditorServices #649](https://github.com/PowerShell/PowerShellEditorServices/pull/649) -
  Get rid of the unneeded exception variable causing a compile warning.

- [PowerShell/PowerShellEditorServices #650](https://github.com/PowerShell/PowerShellEditorServices/pull/650) -
  Add $psEditor.GetEditorContext().CurrentFile.SaveAs("Name") support.

- [PowerShell/PowerShellEditorServices #652](https://github.com/PowerShell/PowerShellEditorServices/pull/652) -
  Make the 'PSESRemoteSessionOpenFile' a support event.

- [PowerShell/PowerShellEditorServices #654](https://github.com/PowerShell/PowerShellEditorServices/pull/654) -
Add customize output color enhancement. Thanks to [KeroroLulu](https://github.com/KeroroLulu) 🎉

## 1.6.0
### Thursday, February 22, 2018

#### Fixes and Improvements

- [PowerShell/vscode-powershell #863](https://github.com/PowerShell/vscode-powershell/issues/863) -
  Eliminate duplicate [DBG] prompt.

- [PowerShell/PowerShellEditorServices #626](https://github.com/PowerShell/PowerShellEditorServices/pull/626) -
  Switch to w3c log file timestamp format.

- [PowerShell/vscode-powershell #907](https://github.com/PowerShell/vscode-powershell/issues/907) -
  Track tempIntegratedConsole launch param, do not exit when session ends.

- [PowerShell/vscode-powershell #1159](https://github.com/PowerShell/vscode-powershell/issues/1159) -
  Fix PSES crash on debug start when function breakpoint defined.

- [PowerShell/PowerShellEditorServices #586](https://github.com/PowerShell/PowerShellEditorServices/issues/586) -
  Add build.ps1 to follow consistent guidelines.

- [PowerShell/PowerShellEditorServices #414](https://github.com/PowerShell/PowerShellEditorServices/issues/414) -
  Enable piping text to `psedit` to open a new untitled buffer.

- [PowerShell/PowerShellEditorServices #413](https://github.com/PowerShell/PowerShellEditorServices/issues/413) -
  Enable piping multiple file paths through `psedit` to open those files.

- [PowerShell/vscode-powershell #1185](https://github.com/PowerShell/vscode-powershell/issues/1185) -
  Fix `PowerShell: Expand Alias` command in macOS.

- [PowerShell/PowerShellEditorServices #612](https://github.com/PowerShell/PowerShellEditorServices/issues/612),
[PowerShell/vscode-powershell](https://github.com/PowerShell/vscode-powershell/issues/908) -
  Fix macOS/linux crash with "too many open files".

- [PowerShell/PowerShellEditorServices #528](https://github.com/PowerShell/PowerShellEditorServices/issues/528) -
  Change psedit to Open-EditorFile and alias psedit to it.

- [PowerShell/PowerShellEditorServices #597](https://github.com/PowerShell/PowerShellEditorServices/issues/597),
[PowerShell/vscode-powershell #789](https://github.com/PowerShell/vscode-powershell/issues/789) -
Fix remote editing in PSCore by fixing *-Content calls in psedit scripts and setting ComputerName default.

- [PowerShell/PowerShellEditorServices #598](https://github.com/PowerShell/PowerShellEditorServices/pull/598) -
  Improve error logging for exec of pscommands.

- [PowerShell/PowerShellEditorServices #594](https://github.com/PowerShell/PowerShellEditorServices/pull/594) -
  Fixed markdown typo to correct the link to the contributing guidelines. Thanks to [dee-see](https://github.com/dee-see)!

- [PowerShell/vscode-powershell #987](https://github.com/PowerShell/vscode-powershell/issues/987),
[PowerShell/vscode-powershell #1107](https://github.com/PowerShell/vscode-powershell/issues/1107),
[PowerShell/PowerShellEditorServices #554](https://github.com/PowerShell/PowerShellEditorServices/issues/554),
[PowerShell/vscode-powershell #1146](https://github.com/PowerShell/vscode-powershell/issues/1146),
[PowerShell/vscode-powershell #1119](https://github.com/PowerShell/vscode-powershell/issues/1119),
[PowerShell/vscode-powershell #120](https://github.com/PowerShell/vscode-powershell/issues/120) -
  Fix debugger step through on Unix platforms.

- [PowerShell/PowerShellEditorServices #590](https://github.com/PowerShell/PowerShellEditorServices/pull/590) -
  Add .Save() to FileContext API.

- [PowerShell/PowerShellEditorServices #588](https://github.com/PowerShell/PowerShellEditorServices/pull/588) -
  Fix bad PSScriptAnalyzer settings path crashes PSES.

- [PowerShell/PowerShellEditorServices #582](https://github.com/PowerShell/PowerShellEditorServices/issues/582) -
  Fix Very Large String crashes PS Editor Services.

- [PowerShell/vscode-powershell #1114](https://github.com/PowerShell/vscode-powershell/issues/1114) -
  Fix breakpoint on nonexisting file.

- [PowerShell/vscode-powershell #1014](https://github.com/PowerShell/vscode-powershell/issues/1014) -
  Fix crash of PSES on startup when workspace folder has [] in path.

## 1.5.1
### Tuesday, November 14, 2017

- [PowerShell/PowerShellEditorServices #574](https://github.com/PowerShell/PowerShellEditorServices/issues/574) -
  Do not attempt to set breakpoints on files other than .ps1 and .psm1.

- [PowerShell/PowerShellEditorServices #570](https://github.com/PowerShell/PowerShellEditorServices/issues/570) -
  Fixed `Get-Help -ShowWindow` error in the PowerShell Integrated Console.  However this fix does not address the issue with
  the help window appearing behind VSCode.

- [PowerShell/PowerShellEditorServices #567](https://github.com/PowerShell/PowerShellEditorServices/issues/567) -
  Fixed off-by-one error in ValidatePosition method.

- [PowerShell/vscode-powershell #1091](https://github.com/PowerShell/vscode-powershell/issues/1091) -
  Fixed crash when editing remote file using psedit by catching PSNotSupportedException.

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
  Temporary debugging session now does not stop responding when running "PowerShell Interactive Session" debugging configuration in VS Code

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
  Fixed an issue where the language server would stop responding when typing out a describe
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
