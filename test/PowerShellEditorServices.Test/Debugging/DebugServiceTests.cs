// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;
using Xunit;

namespace PowerShellEditorServices.Test.Debugging
{
    internal class TestReadLine : IReadLine
    {
        public List<string> history = new();

        public string ReadLine(CancellationToken cancellationToken) => "";

        public void AddToHistory(string historyEntry) => history.Add(historyEntry);
    }

    [Trait("Category", "DebugService")]
    public class DebugServiceTests : IDisposable
    {
        private readonly PsesInternalHost psesHost;
        private readonly BreakpointService breakpointService;
        private readonly DebugService debugService;
        private readonly BlockingCollection<DebuggerStoppedEventArgs> debuggerStoppedQueue = new();
        private readonly WorkspaceService workspace;
        private readonly ScriptFile debugScriptFile;
        private readonly ScriptFile oddPathScriptFile;
        private readonly ScriptFile variableScriptFile;
        private readonly TestReadLine testReadLine = new();

        public DebugServiceTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            // This is required for remote debugging, but we call it here to end up in the same
            // state as the usual startup path.
            psesHost.DebugContext.EnableDebugMode();
            psesHost._readLineProvider.ReadLine = testReadLine;

            breakpointService = new BreakpointService(
                NullLoggerFactory.Instance,
                psesHost,
                psesHost,
                new DebugStateService());

            debugService = new DebugService(
                psesHost,
                psesHost.DebugContext,
                remoteFileManager: null,
                breakpointService,
                psesHost,
                NullLoggerFactory.Instance);

            debugService.DebuggerStopped += OnDebuggerStopped;

            // Load the test debug files.
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
            debugScriptFile = GetDebugScript("DebugTest.ps1");
            oddPathScriptFile = GetDebugScript("Debug' W&ith $Params [Test].ps1");
            variableScriptFile = GetDebugScript("VariableTest.ps1");
        }

        public void Dispose()
        {
            debugService.Abort();
            debuggerStoppedQueue.Dispose();
#pragma warning disable VSTHRD002
            psesHost.StopAsync().Wait();
#pragma warning restore VSTHRD002
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// This event handler lets us test that the debugger stopped or paused as expected. It will
        /// deadlock if called in the PSES Pipeline Thread, which can easily happen in this test
        /// code when methods on <see cref="debugService" /> are called. Hence we treat this test
        /// code like UI code and use 'ConfigureAwait(true)' or 'Task.Run(...)' to ensure we stay
        /// OFF the pipeline thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDebuggerStopped(object sender, DebuggerStoppedEventArgs e) => debuggerStoppedQueue.Add(e);

        private ScriptFile GetDebugScript(string fileName) => workspace.GetFile(TestUtilities.GetSharedPath(Path.Combine("Debugging", fileName)));

        private Task<VariableDetailsBase[]> GetVariables(string scopeName)
        {
            VariableScope scope = Array.Find(
                debugService.GetVariableScopes(0),
                s => s.Name == scopeName);
            return debugService.GetVariables(scope.Id, CancellationToken.None);
        }

        private Task ExecuteScriptFileAsync(string scriptFilePath, params string[] args)
        {
            return psesHost.ExecutePSCommandAsync(
                PSCommandHelpers.BuildDotSourceCommandWithArguments(PSCommandHelpers.EscapeScriptFilePath(scriptFilePath), args),
                CancellationToken.None);
        }

        private Task ExecuteDebugFileAsync() => ExecuteScriptFileAsync(debugScriptFile.FilePath);

        private Task ExecuteVariableScriptFileAsync() => ExecuteScriptFileAsync(variableScriptFile.FilePath);

        private void AssertDebuggerPaused()
        {
            using CancellationTokenSource cts = new(60000);
            DebuggerStoppedEventArgs eventArgs = debuggerStoppedQueue.Take(cts.Token);
            Assert.Empty(eventArgs.OriginalEvent.Breakpoints);
        }

        private void AssertDebuggerStopped(
            string scriptPath = "",
            int lineNumber = -1,
            CommandBreakpointDetails commandBreakpointDetails = default)
        {
            using CancellationTokenSource cts = new(60000);
            DebuggerStoppedEventArgs eventArgs = debuggerStoppedQueue.Take(cts.Token);

            Assert.True(psesHost.DebugContext.IsStopped);

            if (!string.IsNullOrEmpty(scriptPath))
            {
                // TODO: The drive letter becomes lower cased on Windows for some reason.
                Assert.Equal(scriptPath, eventArgs.ScriptPath, ignoreCase: true);
            }
            else
            {
                Assert.Equal(string.Empty, scriptPath);
            }

            if (lineNumber > -1)
            {
                Assert.Equal(lineNumber, eventArgs.LineNumber);
            }

            if (commandBreakpointDetails is not null)
            {
                Assert.Equal(commandBreakpointDetails.Name, eventArgs.OriginalEvent.InvocationInfo.MyCommand.Name);
            }
        }

        private Task<IReadOnlyList<LineBreakpoint>> GetConfirmedBreakpoints(ScriptFile scriptFile)
        {
            // TODO: Should we use the APIs in BreakpointService to get these?
            return psesHost.ExecutePSCommandAsync<LineBreakpoint>(
                new PSCommand().AddCommand("Get-PSBreakpoint").AddParameter("Script", scriptFile.FilePath),
                CancellationToken.None);
        }

        [Fact]
        // This regression test asserts that `ExecuteScriptWithArgsAsync` works for both script
        // files and, in this case, in-line scripts (commands). The bug was that the cwd was
        // erroneously prepended when the script argument was a command.
        public async Task DebuggerAcceptsInlineScript()
        {
            await debugService.SetCommandBreakpointsAsync(
                new[] { CommandBreakpointDetails.Create("Get-Random") });

            Task<IReadOnlyList<int>> executeTask = psesHost.ExecutePSCommandAsync<int>(
                new PSCommand().AddScript("Get-Random -SetSeed 42 -Maximum 100"), CancellationToken.None);

            AssertDebuggerStopped("", 1);
            await Task.Run(debugService.Continue);
            Assert.Equal(17, (await executeTask)[0]);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync();
            Assert.Equal(StackFrameDetails.NoFileScriptPath, stackFrames[0].ScriptPath);

            // NOTE: This assertion will fail if any error occurs. Notably this happens in testing
            // when the assembly path changes and the commands definition file can't be found.
            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.GlobalScopeName);
            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$Error");
            Assert.NotNull(var);
            Assert.True(var.IsExpandable);
            Assert.Equal("[ArrayList: 0]", var.ValueString);
        }

        // See https://www.thomasbogholm.net/2021/06/01/convenient-member-data-sources-with-xunit/
        public static IEnumerable<object[]> DebuggerAcceptsScriptArgsTestData => new List<object[]>()
        {
            new object[] { new object[] { "Foo -Param2 @('Bar','Baz') -Force Extra1" } },
            new object[] { new object[] { "Foo", "-Param2", "@('Bar','Baz')", "-Force", "Extra1" } }
        };

        [Theory]
        [MemberData(nameof(DebuggerAcceptsScriptArgsTestData))]
        public async Task DebuggerAcceptsScriptArgs(string[] args)
        {
            IReadOnlyList<BreakpointDetails> breakpoints = await debugService.SetLineBreakpointsAsync(
                oddPathScriptFile,
                new[] { BreakpointDetails.Create(oddPathScriptFile.FilePath, 3) });

            Assert.Single(breakpoints);
            Assert.Collection(breakpoints, (breakpoint) =>
            {
                // TODO: The drive letter becomes lower cased on Windows for some reason.
                Assert.Equal(oddPathScriptFile.FilePath, breakpoint.Source, ignoreCase: true);
                Assert.Equal(3, breakpoint.LineNumber);
                Assert.True(breakpoint.Verified);
            });

            Task _ = ExecuteScriptFileAsync(oddPathScriptFile.FilePath, args);

            AssertDebuggerStopped(oddPathScriptFile.FilePath, 3);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$Param1");
            Assert.NotNull(var);
            Assert.Equal("\"Foo\"", var.ValueString);
            Assert.False(var.IsExpandable);

            var = Array.Find(variables, v => v.Name == "$Param2");
            Assert.NotNull(var);
            Assert.True(var.IsExpandable);

            VariableDetailsBase[] childVars = await debugService.GetVariables(var.Id, CancellationToken.None);
            // 2 variables plus "Raw View"
            Assert.Equal(3, childVars.Length);
            Assert.Equal("\"Bar\"", childVars[0].ValueString);
            Assert.Equal("\"Baz\"", childVars[1].ValueString);

            var = Array.Find(variables, v => v.Name == "$Force");
            Assert.NotNull(var);
            Assert.Equal("True", var.ValueString);
            Assert.True(var.IsExpandable);

            // NOTE: $args are no longer found in AutoVariables but CommandVariables instead.
            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync();
            variables = await debugService.GetVariables(stackFrames[0].CommandVariables.Id, CancellationToken.None);
            var = Array.Find(variables, v => v.Name == "$args");
            Assert.NotNull(var);
            Assert.True(var.IsExpandable);

            childVars = await debugService.GetVariables(var.Id, CancellationToken.None);
            Assert.Equal(2, childVars.Length);
            Assert.Equal("\"Extra1\"", childVars[0].ValueString);
        }

        [Fact]
        public async Task DebuggerSetsAndClearsFunctionBreakpoints()
        {
            IReadOnlyList<CommandBreakpointDetails> breakpoints = await debugService.SetCommandBreakpointsAsync(
                new[] {
                    CommandBreakpointDetails.Create("Write-Host"),
                    CommandBreakpointDetails.Create("Get-Date")
                });

            Assert.Equal(2, breakpoints.Count);
            Assert.Equal("Write-Host", breakpoints[0].Name);
            Assert.Equal("Get-Date", breakpoints[1].Name);

            breakpoints = await debugService.SetCommandBreakpointsAsync(
                new[] { CommandBreakpointDetails.Create("Get-Host") });

            Assert.Equal("Get-Host", Assert.Single(breakpoints).Name);

            breakpoints = await debugService.SetCommandBreakpointsAsync(
                Array.Empty<CommandBreakpointDetails>());

            Assert.Empty(breakpoints);
        }

        [Fact]
        public async Task DebuggerStopsOnFunctionBreakpoints()
        {
            IReadOnlyList<CommandBreakpointDetails> breakpoints = await debugService.SetCommandBreakpointsAsync(
                new[] { CommandBreakpointDetails.Create("Write-Host") });

            Task _ = ExecuteDebugFileAsync();
            AssertDebuggerStopped(debugScriptFile.FilePath, 6);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the function breakpoint broke at Write-Host and $i is 1
            VariableDetailsBase i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal("1", i.ValueString);

            // The function breakpoint should fire the next time through the loop.
            await Task.Run(debugService.Continue);
            AssertDebuggerStopped(debugScriptFile.FilePath, 6);

            variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the function breakpoint broke at Write-Host and $i is 1
            i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal("2", i.ValueString);
        }

        [Fact]
        public async Task DebuggerSetsAndClearsLineBreakpoints()
        {
            IReadOnlyList<BreakpointDetails> breakpoints =
                await debugService.SetLineBreakpointsAsync(
                    debugScriptFile,
                    new[] {
                        BreakpointDetails.Create(debugScriptFile.FilePath, 5),
                        BreakpointDetails.Create(debugScriptFile.FilePath, 10)
                    });

            IReadOnlyList<LineBreakpoint> confirmedBreakpoints = await GetConfirmedBreakpoints(debugScriptFile);

            Assert.Equal(2, confirmedBreakpoints.Count);
            Assert.Equal(5, breakpoints[0].LineNumber);
            Assert.Equal(10, breakpoints[1].LineNumber);

            breakpoints = await debugService.SetLineBreakpointsAsync(
                debugScriptFile,
                new[] { BreakpointDetails.Create(debugScriptFile.FilePath, 2) });
            confirmedBreakpoints = await GetConfirmedBreakpoints(debugScriptFile);

            Assert.Single(confirmedBreakpoints);
            Assert.Equal(2, breakpoints[0].LineNumber);

            await debugService.SetLineBreakpointsAsync(
                debugScriptFile,
                Array.Empty<BreakpointDetails>());

            IReadOnlyList<LineBreakpoint> remainingBreakpoints = await GetConfirmedBreakpoints(debugScriptFile);
            Assert.Empty(remainingBreakpoints);
        }

        [Fact]
        public async Task DebuggerStopsOnLineBreakpoints()
        {
            await debugService.SetLineBreakpointsAsync(
                debugScriptFile,
                new[] {
                    BreakpointDetails.Create(debugScriptFile.FilePath, 5),
                    BreakpointDetails.Create(debugScriptFile.FilePath, 7)
                });

            Task _ = ExecuteDebugFileAsync();
            AssertDebuggerStopped(debugScriptFile.FilePath, 5);
            await Task.Run(debugService.Continue);
            AssertDebuggerStopped(debugScriptFile.FilePath, 7);
        }

        [Fact]
        public async Task DebuggerStopsOnConditionalBreakpoints()
        {
            const int breakpointValue1 = 10;
            const int breakpointValue2 = 20;

            await debugService.SetLineBreakpointsAsync(
                debugScriptFile,
                new[] {
                    BreakpointDetails.Create(debugScriptFile.FilePath, 7, null, $"$i -eq {breakpointValue1} -or $i -eq {breakpointValue2}"),
                });

            Task _ = ExecuteDebugFileAsync();
            AssertDebuggerStopped(debugScriptFile.FilePath, 7);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            VariableDetailsBase i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal($"{breakpointValue1}", i.ValueString);

            // The conditional breakpoint should not fire again, until the value of
            // i reaches breakpointValue2.
            await Task.Run(debugService.Continue);
            AssertDebuggerStopped(debugScriptFile.FilePath, 7);

            variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal($"{breakpointValue2}", i.ValueString);
        }

        [Fact]
        public async Task DebuggerStopsOnHitConditionBreakpoint()
        {
            const int hitCount = 5;

            await debugService.SetLineBreakpointsAsync(
                debugScriptFile,
                new[] {
                    BreakpointDetails.Create(debugScriptFile.FilePath, 6, null, null, $"{hitCount}"),
                });

            Task _ = ExecuteDebugFileAsync();
            AssertDebuggerStopped(debugScriptFile.FilePath, 6);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            VariableDetailsBase i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal($"{hitCount}", i.ValueString);
        }

        [Fact]
        public async Task DebuggerStopsOnConditionalAndHitConditionBreakpoint()
        {
            const int hitCount = 5;

            await debugService.SetLineBreakpointsAsync(
                debugScriptFile,
                new[] { BreakpointDetails.Create(debugScriptFile.FilePath, 6, null, "$i % 2 -eq 0", $"{hitCount}") });

            Task _ = ExecuteDebugFileAsync();
            AssertDebuggerStopped(debugScriptFile.FilePath, 6);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            VariableDetailsBase i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            // Condition is even numbers ($i starting at 1) should end up on 10 with a hit count of 5.
            Assert.Equal("10", i.ValueString);
        }

        [Fact]
        public async Task DebuggerProvidesMessageForInvalidConditionalBreakpoint()
        {
            IReadOnlyList<BreakpointDetails> breakpoints =
                await debugService.SetLineBreakpointsAsync(
                    debugScriptFile,
                    new[] {
                        // TODO: Add this breakpoint back when it stops moving around?! The ordering
                        // of these two breakpoints seems to do with which framework executes the
                        // code. Best guess is that `IEnumerable` is not stably sorted so `ToArray`
                        // returns different orderings. However, that doesn't explain why this is
                        // the only affected test.

                        // BreakpointDetails.Create(debugScriptFile.FilePath, 5),
                        BreakpointDetails.Create(debugScriptFile.FilePath, 10, column: null, condition: "$i -ez 100")
                    });

            Assert.Single(breakpoints);
            // Assert.Equal(5, breakpoints[0].LineNumber);
            // Assert.True(breakpoints[0].Verified);
            // Assert.Null(breakpoints[0].Message);

            Assert.Equal(10, breakpoints[0].LineNumber);
            Assert.False(breakpoints[0].Verified);
            Assert.NotNull(breakpoints[0].Message);
            Assert.Contains("Unexpected token '-ez'", breakpoints[0].Message);
        }

        [Fact]
        public async Task DebuggerFindsParsableButInvalidSimpleBreakpointConditions()
        {
            IReadOnlyList<BreakpointDetails> breakpoints =
                await debugService.SetLineBreakpointsAsync(
                    debugScriptFile,
                    new[] {
                        BreakpointDetails.Create(debugScriptFile.FilePath, 5, column: null, condition: "$i == 100"),
                        BreakpointDetails.Create(debugScriptFile.FilePath, 7, column: null, condition: "$i > 100")
                    });

            Assert.Equal(2, breakpoints.Count);
            Assert.Equal(5, breakpoints[0].LineNumber);
            Assert.False(breakpoints[0].Verified);
            Assert.Contains("Use '-eq' instead of '=='", breakpoints[0].Message);

            Assert.Equal(7, breakpoints[1].LineNumber);
            Assert.False(breakpoints[1].Verified);
            Assert.NotNull(breakpoints[1].Message);
            Assert.Contains("Use '-gt' instead of '>'", breakpoints[1].Message);
        }

        [Fact]
        public async Task DebuggerBreaksWhenRequested()
        {
            IReadOnlyList<LineBreakpoint> confirmedBreakpoints = await GetConfirmedBreakpoints(debugScriptFile);
            Assert.Empty(confirmedBreakpoints);
            Task _ = ExecuteDebugFileAsync();
            // NOTE: This must be run on a separate thread so the async event handlers can fire.
            await Task.Run(debugService.Break);
            AssertDebuggerPaused();
        }

        [Fact]
        public async Task DebuggerRunsCommandsWhileStopped()
        {
            Task _ = ExecuteDebugFileAsync();
            // NOTE: This must be run on a separate thread so the async event handlers can fire.
            await Task.Run(debugService.Break);
            AssertDebuggerPaused();

            // Try running a command from outside the pipeline thread
            Task<IReadOnlyList<int>> executeTask = psesHost.ExecutePSCommandAsync<int>(
                new PSCommand().AddScript("Get-Random -SetSeed 42 -Maximum 100"), CancellationToken.None);
            Assert.Equal(17, (await executeTask)[0]);
        }

        // Regression test asserting that the PSDebugContext variable is available when running the
        // "prompt" function. While we're unable to test the REPL loop, this still covers the
        // behavior as I verified that it stepped through "ExecuteInDebugger" (which was the
        // original problem).
        [Fact]
        public async Task DebugContextAvailableInPrompt()
        {
            await debugService.SetCommandBreakpointsAsync(
                new[] { CommandBreakpointDetails.Create("Write-Host") });

            ScriptFile testScript = GetDebugScript("PSDebugContextTest.ps1");
            Task _ = ExecuteScriptFileAsync(testScript.FilePath);
            AssertDebuggerStopped(testScript.FilePath, 11);

            VariableDetails prompt = await debugService.EvaluateExpressionAsync("prompt", false, CancellationToken.None);
            Assert.Equal("True > ", prompt.ValueString);
        }

        [SkippableFact]
        public async Task DebuggerBreaksInUntitledScript()
        {
            Skip.IfNot(VersionUtils.PSEdition == "Core", "Untitled script breakpoints only supported in PowerShell Core");
            const string contents = "Write-Output $($MyInvocation.Line)";
            const string scriptPath = "untitled:Untitled-1";
            Assert.True(ScriptFile.IsUntitledPath(scriptPath));
            ScriptFile scriptFile = workspace.GetFileBuffer(scriptPath, contents);
            Assert.Equal(scriptPath, scriptFile.DocumentUri);
            Assert.Equal(contents, scriptFile.Contents);
            Assert.True(workspace.TryGetFile(scriptPath, out ScriptFile _));

            await debugService.SetCommandBreakpointsAsync(
                new[] { CommandBreakpointDetails.Create("Write-Output") });

            ConfigurationDoneHandler configurationDoneHandler = new(
                NullLoggerFactory.Instance, null, debugService, null, null, psesHost, workspace, null, psesHost);

            Task _ = configurationDoneHandler.LaunchScriptAsync(scriptPath);
            AssertDebuggerStopped(scriptPath, 1);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.CommandVariablesName);
            VariableDetailsBase myInvocation = Array.Find(variables, v => v.Name == "$MyInvocation");
            Assert.NotNull(myInvocation);
            Assert.True(myInvocation.IsExpandable);

            // Here we're asserting that our hacky workaround to support breakpoints in untitled
            // scripts is working, namely that we're actually dot-sourcing our first argument, which
            // should be a cached script block. See the `LaunchScriptAsync` for more info.
            VariableDetailsBase[] myInvocationChildren = await debugService.GetVariables(myInvocation.Id, CancellationToken.None);
            VariableDetailsBase myInvocationLine = Array.Find(myInvocationChildren, v => v.Name == "Line");
            Assert.Equal("\". $args[0]\"", myInvocationLine.ValueString);
        }

        [Fact]
        public async Task RecordsF5CommandInPowerShellHistory()
        {
            ConfigurationDoneHandler configurationDoneHandler = new(
                NullLoggerFactory.Instance, null, debugService, null, null, psesHost, workspace, null, psesHost);
            await configurationDoneHandler.LaunchScriptAsync(debugScriptFile.FilePath);

            IReadOnlyList<string> historyResult = await psesHost.ExecutePSCommandAsync<string>(
                new PSCommand().AddScript("(Get-History).CommandLine"),
                CancellationToken.None);

            // Check the PowerShell history
            Assert.Equal(". '" + debugScriptFile.FilePath + "'", Assert.Single(historyResult));

            // Check the stubbed PSReadLine history
            Assert.Equal(". '" + debugScriptFile.FilePath + "'", Assert.Single(testReadLine.history));
        }

        [Fact]
        public async Task RecordsF8CommandInHistory()
        {
            const string script = "Write-Output Hello";
            EvaluateHandler evaluateHandler = new(psesHost);
            EvaluateResponseBody evaluateResponseBody = await evaluateHandler.Handle(
                new EvaluateRequestArguments { Expression = script, Context = "repl" },
                CancellationToken.None);
            // TODO: Right now this response is hard-coded, maybe it should change?
            Assert.Equal("", evaluateResponseBody.Result);

            IReadOnlyList<string> historyResult = await psesHost.ExecutePSCommandAsync<string>(
                new PSCommand().AddScript("(Get-History).CommandLine"),
                CancellationToken.None);

            // Check the PowerShell history
            Assert.Equal(script, Assert.Single(historyResult));

            // Check the stubbed PSReadLine history
            Assert.Equal(script, Assert.Single(testReadLine.history));
        }

        [Fact]
        public async Task OddFilePathsLaunchCorrectly()
        {
            ConfigurationDoneHandler configurationDoneHandler = new(
                NullLoggerFactory.Instance, null, debugService, null, null, psesHost, workspace, null, psesHost);
            await configurationDoneHandler.LaunchScriptAsync(oddPathScriptFile.FilePath);

            IReadOnlyList<string> historyResult = await psesHost.ExecutePSCommandAsync<string>(
                new PSCommand().AddScript("(Get-History).CommandLine"),
                CancellationToken.None);

            // Check the PowerShell history
            Assert.Equal(". " + PSCommandHelpers.EscapeScriptFilePath(oddPathScriptFile.FilePath), Assert.Single(historyResult));
        }

        [Fact]
        public async Task DebuggerVariableStringDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 8) });

            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$strVar");
            Assert.NotNull(var);
            Assert.Equal("\"Hello\"", var.ValueString);
            Assert.False(var.IsExpandable);
        }

        [Fact]
        public async Task DebuggerGetsVariables()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 21) });

            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // TODO: Add checks for correct value strings as well
            VariableDetailsBase strVar = Array.Find(variables, v => v.Name == "$strVar");
            Assert.NotNull(strVar);
            Assert.False(strVar.IsExpandable);

            VariableDetailsBase objVar = Array.Find(variables, v => v.Name == "$assocArrVar");
            Assert.NotNull(objVar);
            Assert.True(objVar.IsExpandable);

            VariableDetailsBase[] objChildren = await debugService.GetVariables(objVar.Id, CancellationToken.None);
            // Two variables plus "Raw View"
            Assert.Equal(3, objChildren.Length);

            VariableDetailsBase arrVar = Array.Find(variables, v => v.Name == "$arrVar");
            Assert.NotNull(arrVar);
            Assert.True(arrVar.IsExpandable);

            VariableDetailsBase[] arrChildren = await debugService.GetVariables(arrVar.Id, CancellationToken.None);
            Assert.Equal(5, arrChildren.Length);

            VariableDetailsBase classVar = Array.Find(variables, v => v.Name == "$classVar");
            Assert.NotNull(classVar);
            Assert.True(classVar.IsExpandable);

            VariableDetailsBase[] classChildren = await debugService.GetVariables(classVar.Id, CancellationToken.None);
            Assert.Equal(2, classChildren.Length);

            VariableDetailsBase trueVar = Array.Find(variables, v => v.Name == "$trueVar");
            Assert.NotNull(trueVar);
            Assert.Equal("boolean", trueVar.Type);
            Assert.Equal("$true", trueVar.ValueString);

            VariableDetailsBase falseVar = Array.Find(variables, v => v.Name == "$falseVar");
            Assert.NotNull(falseVar);
            Assert.Equal("boolean", falseVar.Type);
            Assert.Equal("$false", falseVar.ValueString);
        }

        [Fact]
        public async Task DebuggerSetsVariablesNoConversion()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 14) });

            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            VariableScope[] scopes = debugService.GetVariableScopes(0);
            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // Test set of a local string variable (not strongly typed)
            const string newStrValue = "\"Goodbye\"";
            VariableScope localScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.LocalScopeName);
            string setStrValue = await debugService.SetVariableAsync(localScope.Id, "$strVar", newStrValue);
            Assert.Equal(newStrValue, setStrValue);

            // Test set of script scope int variable (not strongly typed)
            VariableScope scriptScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.ScriptScopeName);
            const string newIntValue = "49";
            const string newIntExpr = "7 * 7";
            string setIntValue = await debugService.SetVariableAsync(scriptScope.Id, "$scriptInt", newIntExpr);
            Assert.Equal(newIntValue, setIntValue);

            // Test set of global scope int variable (not strongly typed)
            VariableScope globalScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.GlobalScopeName);
            const string newGlobalIntValue = "4242";
            string setGlobalIntValue = await debugService.SetVariableAsync(globalScope.Id, "$MaximumHistoryCount", newGlobalIntValue);
            Assert.Equal(newGlobalIntValue, setGlobalIntValue);

            // The above just tests that the debug service returns the correct new value string.
            // Let's step the debugger and make sure the values got set to the new values.
            await Task.Run(debugService.StepOver);
            AssertDebuggerStopped(variableScriptFile.FilePath);

            // Test set of a local string variable (not strongly typed)
            variables = await GetVariables(VariableContainerDetails.LocalScopeName);
            VariableDetailsBase strVar = Array.Find(variables, v => v.Name == "$strVar");
            Assert.Equal(newStrValue, strVar.ValueString);

            // Test set of script scope int variable (not strongly typed)
            variables = await GetVariables(VariableContainerDetails.ScriptScopeName);
            VariableDetailsBase intVar = Array.Find(variables, v => v.Name == "$scriptInt");
            Assert.Equal(newIntValue, intVar.ValueString);

            // Test set of global scope int variable (not strongly typed)
            variables = await GetVariables(VariableContainerDetails.GlobalScopeName);
            VariableDetailsBase intGlobalVar = Array.Find(variables, v => v.Name == "$MaximumHistoryCount");
            Assert.Equal(newGlobalIntValue, intGlobalVar.ValueString);
        }

        [Fact]
        public async Task DebuggerSetsVariablesWithConversion()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 14) });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            VariableScope[] scopes = debugService.GetVariableScopes(0);
            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);

            // Test set of a local string variable (not strongly typed but force conversion)
            const string newStrValue = "\"False\"";
            const string newStrExpr = "$false";
            VariableScope localScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.LocalScopeName);
            string setStrValue = await debugService.SetVariableAsync(localScope.Id, "$strVar2", newStrExpr);
            Assert.Equal(newStrValue, setStrValue);

            // Test set of script scope bool variable (strongly typed)
            VariableScope scriptScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.ScriptScopeName);
            const string newBoolValue = "$true";
            const string newBoolExpr = "1";
            string setBoolValue = await debugService.SetVariableAsync(scriptScope.Id, "$scriptBool", newBoolExpr);
            Assert.Equal(newBoolValue, setBoolValue);

            // Test set of global scope ActionPreference variable (strongly typed)
            VariableScope globalScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.GlobalScopeName);
            const string newGlobalValue = "Continue";
            const string newGlobalExpr = "'Continue'";
            string setGlobalValue = await debugService.SetVariableAsync(globalScope.Id, "$VerbosePreference", newGlobalExpr);
            Assert.Equal(newGlobalValue, setGlobalValue);

            // The above just tests that the debug service returns the correct new value string.
            // Let's step the debugger and make sure the values got set to the new values.
            await Task.Run(debugService.StepOver);
            AssertDebuggerStopped(variableScriptFile.FilePath);

            // Test set of a local string variable (not strongly typed but force conversion)
            variables = await GetVariables(VariableContainerDetails.LocalScopeName);
            VariableDetailsBase strVar = Array.Find(variables, v => v.Name == "$strVar2");
            Assert.Equal(newStrValue, strVar.ValueString);

            // Test set of script scope bool variable (strongly typed)
            variables = await GetVariables(VariableContainerDetails.ScriptScopeName);
            VariableDetailsBase boolVar = Array.Find(variables, v => v.Name == "$scriptBool");
            Assert.Equal(newBoolValue, boolVar.ValueString);

            // Test set of global scope ActionPreference variable (strongly typed)
            variables = await GetVariables(VariableContainerDetails.GlobalScopeName);
            VariableDetailsBase globalVar = Array.Find(variables, v => v.Name == "$VerbosePreference");
            Assert.Equal(newGlobalValue, globalVar.ValueString);
        }

        [Fact]
        public async Task DebuggerVariableEnumDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 15) });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync();
            VariableDetailsBase[] variables = await debugService.GetVariables(stackFrames[0].AutoVariables.Id, CancellationToken.None);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$enumVar");
            Assert.NotNull(var);
            Assert.Equal("Continue", var.ValueString);
            Assert.False(var.IsExpandable);
        }

        [Fact]
        public async Task DebuggerVariableHashtableDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 11) });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync();
            VariableDetailsBase[] variables = await debugService.GetVariables(stackFrames[0].AutoVariables.Id, CancellationToken.None);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$assocArrVar");
            Assert.NotNull(var);
            Assert.Equal("[Hashtable: 2]", var.ValueString);
            Assert.True(var.IsExpandable);

            VariableDetailsBase[] childVars = await debugService.GetVariables(var.Id, CancellationToken.None);
            // 2 variables plus "Raw View"
            Assert.Equal(3, childVars.Length);

            // Hashtables are unordered hence the Linq examination, examination by index is unreliable
            VariableDetailsBase firstChild = Array.Find(childVars, v => v.Name == "[firstChild]");
            Assert.NotNull(firstChild);
            Assert.Equal("\"Child\"", firstChild.ValueString);

            VariableDetailsBase secondChild = Array.Find(childVars, v => v.Name == "[secondChild]");
            Assert.NotNull(secondChild);
            Assert.Equal("42", secondChild.ValueString);
        }

        [Fact]
        public async Task DebuggerVariableNullStringDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 16) });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync();
            VariableDetailsBase[] variables = await debugService.GetVariables(stackFrames[0].AutoVariables.Id, CancellationToken.None);

            VariableDetailsBase nullStringVar = Array.Find(variables, v => v.Name == "$nullString");
            Assert.NotNull(nullStringVar);
            Assert.Equal("[NullString]", nullStringVar.ValueString);
            Assert.True(nullStringVar.IsExpandable);
        }

        [Fact]
        public async Task DebuggerVariablePSObjectDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 17) });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync();
            VariableDetailsBase[] variables = await debugService.GetVariables(stackFrames[0].AutoVariables.Id, CancellationToken.None);

            VariableDetailsBase psObjVar = Array.Find(variables, v => v.Name == "$psObjVar");
            Assert.NotNull(psObjVar);
            Assert.True("@{Age=75; Name=John}".Equals(psObjVar.ValueString) || "@{Name=John; Age=75}".Equals(psObjVar.ValueString));
            Assert.True(psObjVar.IsExpandable);

            VariableDetailsBase[] childVars = await debugService.GetVariables(psObjVar.Id, CancellationToken.None);
            IDictionary<string, string> dictionary = childVars.ToDictionary(v => v.Name, v => v.ValueString);
            Assert.Equal(2, dictionary.Count);
            Assert.Contains("Age", dictionary.Keys);
            Assert.Contains("Name", dictionary.Keys);
            Assert.Equal("75", dictionary["Age"]);
            Assert.Equal("\"John\"", dictionary["Name"]);
        }

        [Fact]
        public async Task DebuggerEnumerableShowsRawView()
        {
            CommandBreakpointDetails breakpoint = CommandBreakpointDetails.Create("__BreakDebuggerEnumerableShowsRawView");
            await debugService.SetCommandBreakpointsAsync(new[] { breakpoint });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(commandBreakpointDetails: breakpoint);

            VariableDetailsBase simpleArrayVar = Array.Find(
                await GetVariables(VariableContainerDetails.ScriptScopeName),
                v => v.Name == "$simpleArray");
            Assert.NotNull(simpleArrayVar);
            VariableDetailsBase rawDetailsView = Array.Find(
                simpleArrayVar.GetChildren(NullLogger.Instance),
                v => v.Name == "Raw View");
            Assert.NotNull(rawDetailsView);
            Assert.Empty(rawDetailsView.Type);
            Assert.Empty(rawDetailsView.ValueString);
            VariableDetailsBase[] rawViewChildren = rawDetailsView.GetChildren(NullLogger.Instance);
            Assert.Collection(rawViewChildren,
                (i) =>
                {
                    Assert.Equal("Length", i.Name);
                    Assert.Equal("4", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("LongLength", i.Name);
                    Assert.Equal("4", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("Rank", i.Name);
                    Assert.Equal("1", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("SyncRoot", i.Name);
                    Assert.True(i.IsExpandable);
                },
                (i) =>
                {
                    Assert.Equal("IsReadOnly", i.Name);
                    Assert.Equal("$false", i.ValueString);
                }, (i) =>
                {
                    Assert.Equal("IsFixedSize", i.Name);
                    Assert.Equal("$true", i.ValueString);
                }, (i) =>
                {
                    Assert.Equal("IsSynchronized", i.Name);
                    Assert.Equal("$false", i.ValueString);
                });
        }

        [Fact]
        public async Task DebuggerDictionaryShowsRawView()
        {
            CommandBreakpointDetails breakpoint = CommandBreakpointDetails.Create("__BreakDebuggerDictionaryShowsRawView");
            await debugService.SetCommandBreakpointsAsync(new[] { breakpoint });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(commandBreakpointDetails: breakpoint);

            VariableDetailsBase simpleDictionaryVar = Array.Find(
                await GetVariables(VariableContainerDetails.ScriptScopeName),
                v => v.Name == "$simpleDictionary");
            Assert.NotNull(simpleDictionaryVar);
            VariableDetailsBase rawDetailsView = Array.Find(
                simpleDictionaryVar.GetChildren(NullLogger.Instance),
                v => v.Name == "Raw View");
            Assert.NotNull(rawDetailsView);
            Assert.Empty(rawDetailsView.Type);
            Assert.Empty(rawDetailsView.ValueString);
            VariableDetailsBase[] rawDetailsChildren = rawDetailsView.GetChildren(NullLogger.Instance);
            Assert.Collection(rawDetailsChildren,
                (i) =>
                {
                    Assert.Equal("IsReadOnly", i.Name);
                    Assert.Equal("$false", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("IsFixedSize", i.Name);
                    Assert.Equal("$false", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("IsSynchronized", i.Name);
                    Assert.Equal("$false", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("Keys", i.Name);
                    Assert.Equal("[KeyCollection: 4]", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("Values", i.Name);
                    Assert.Equal("[ValueCollection: 4]", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("SyncRoot", i.Name);
#if CoreCLR
                    Assert.Equal("[Hashtable: 4]", i.ValueString);
#else
                    Assert.Equal("[Object]", i.ValueString);
#endif
                },
                (i) =>
                {
                    Assert.Equal("Count", i.Name);
                    Assert.Equal("4", i.ValueString);
                });
        }

        [Fact]
        public async Task DebuggerDerivedDictionaryPropertyInRawView()
        {
            CommandBreakpointDetails breakpoint = CommandBreakpointDetails.Create("__BreakDebuggerDerivedDictionaryPropertyInRawView");
            await debugService.SetCommandBreakpointsAsync(new[] { breakpoint });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(commandBreakpointDetails: breakpoint);

            VariableDetailsBase sortedDictionaryVar = Array.Find(
                await GetVariables(VariableContainerDetails.ScriptScopeName),
                v => v.Name == "$sortedDictionary");
            Assert.NotNull(sortedDictionaryVar);
            VariableDetailsBase[] simpleDictionaryChildren = sortedDictionaryVar.GetChildren(NullLogger.Instance);
            // 4 items + Raw View
            Assert.Equal(5, simpleDictionaryChildren.Length);
            VariableDetailsBase rawDetailsView = Array.Find(
                simpleDictionaryChildren,
                v => v.Name == "Raw View");
            Assert.NotNull(rawDetailsView);
            Assert.Empty(rawDetailsView.Type);
            Assert.Empty(rawDetailsView.ValueString);
            VariableDetailsBase[] rawViewChildren = rawDetailsView.GetChildren(NullLogger.Instance);
            Assert.Collection(rawViewChildren,
                (i) =>
                {
                    Assert.Equal("Count", i.Name);
                    Assert.Equal("4", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("Comparer", i.Name);
                    Assert.Equal("[GenericComparer`1]", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("Keys", i.Name);
                    Assert.Equal("[KeyCollection: 4]", i.ValueString);
                },
                (i) =>
                {
                    Assert.Equal("Values", i.Name);
                    Assert.Equal("[ValueCollection: 4]", i.ValueString);
                }
            );
        }

        [Fact]
        public async Task DebuggerVariablePSCustomObjectDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 18) });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync();
            VariableDetailsBase[] variables = await debugService.GetVariables(stackFrames[0].AutoVariables.Id, CancellationToken.None);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$psCustomObjVar");
            Assert.NotNull(var);
            Assert.Equal("@{Name=Paul; Age=73}", var.ValueString);
            Assert.True(var.IsExpandable);

            VariableDetailsBase[] childVars = await debugService.GetVariables(var.Id, CancellationToken.None);
            Assert.Equal(2, childVars.Length);
            Assert.Equal("Name", childVars[0].Name);
            Assert.Equal("\"Paul\"", childVars[0].ValueString);
            Assert.Equal("Age", childVars[1].Name);
            Assert.Equal("73", childVars[1].ValueString);
        }

        // Verifies fix for issue #86, $proc = Get-Process foo displays just the ETS property set
        // and not all process properties.
        [Fact]
        public async Task DebuggerVariableProcessObjectDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 19) });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync();
            VariableDetailsBase[] variables = await debugService.GetVariables(stackFrames[0].AutoVariables.Id, CancellationToken.None);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$procVar");
            Assert.NotNull(var);
            Assert.StartsWith("System.Diagnostics.Process", var.ValueString);
            Assert.True(var.IsExpandable);

            VariableDetailsBase[] childVars = await debugService.GetVariables(var.Id, CancellationToken.None);
            Assert.Contains(childVars, i => i.Name is "Name");
            Assert.Contains(childVars, i => i.Name is "Handles");
#if CoreCLR
            Assert.Contains(childVars, i => i.Name is "CommandLine");
            Assert.Contains(childVars, i => i.Name is "ExitCode");
            Assert.Contains(childVars, i => i.Name is "HasExited" && i.ValueString is "$false");
#endif
            Assert.Contains(childVars, i => i.Name is "Id");
        }

        [Fact]
        public async Task DebuggerVariableFileObjectDisplaysCorrectly()
        {
            await debugService.SetCommandBreakpointsAsync(
                new[] { CommandBreakpointDetails.Create("Write-Host") });

            ScriptFile testScript = GetDebugScript("GetChildItemTest.ps1");
            Task _ = ExecuteScriptFileAsync(testScript.FilePath);
            AssertDebuggerStopped(testScript.FilePath, 2);

            VariableDetailsBase[] variables = await GetVariables(VariableContainerDetails.LocalScopeName);
            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$file");
            VariableDetailsBase[] childVars = await debugService.GetVariables(var.Id, CancellationToken.None);
            Assert.Contains(childVars, i => i.Name is "PSPath");
            Assert.Contains(childVars, i => i.Name is "PSProvider" && i.ValueString is @"Microsoft.PowerShell.Core\FileSystem");
            Assert.Contains(childVars, i => i.Name is "Exists" && i.ValueString is "$true");
            Assert.Contains(childVars, i => i.Name is "LastAccessTime");
        }

        // Verifies Issue #1686
        [Fact]
        public async Task DebuggerToStringShouldMarshallToPipeline()
        {
            CommandBreakpointDetails breakpoint = CommandBreakpointDetails.Create("__BreakDebuggerToStringShouldMarshallToPipeline");
            await debugService.SetCommandBreakpointsAsync(new[] { breakpoint });

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFileAsync();
            AssertDebuggerStopped(commandBreakpointDetails: breakpoint);

            VariableDetailsBase[] vars = await GetVariables(VariableContainerDetails.ScriptScopeName);
            VariableDetailsBase customToStrings = Array.Find(vars, i => i.Name is "$CustomToStrings");
            Assert.True(customToStrings.IsExpandable);
            Assert.Equal("[System.Object[]]", customToStrings.Type);
            VariableDetailsBase[] childVars = await debugService.GetVariables(customToStrings.Id, CancellationToken.None);
            // Check everything but the last variable (which is "Raw View")
            Assert.Equal(1001, childVars.Length); // 1000 custom variables plus "Raw View"
            Assert.All(childVars.Take(childVars.Length - 1), i =>
            {
                Assert.Equal("HELLO", i.ValueString);
                Assert.Equal("[CustomToString]", i.Type);
            });
        }
    }
}
