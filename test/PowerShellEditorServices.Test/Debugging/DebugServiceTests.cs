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
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;
using Xunit;
namespace Microsoft.PowerShell.EditorServices.Test.Debugging
{
    [Trait("Category", "DebugService")]
    public class DebugServiceTests : IDisposable
    {
        private readonly PsesInternalHost psesHost;
        private readonly BreakpointService breakpointService;
        private readonly DebugService debugService;
        private readonly BlockingCollection<DebuggerStoppedEventArgs> debuggerStoppedQueue = new();
        private readonly WorkspaceService workspace;
        private readonly ScriptFile debugScriptFile;
        private readonly ScriptFile variableScriptFile;

        public DebugServiceTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            // This is required for remote debugging, but we call it here to end up in the same
            // state as the usual startup path.
            psesHost.DebugContext.EnableDebugMode();

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

            // Load the test debug files
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
            debugScriptFile = GetDebugScript("DebugTest.ps1");
            variableScriptFile = GetDebugScript("VariableTest.ps1");
        }

        public void Dispose()
        {
            debugService.Abort();
            debuggerStoppedQueue.Dispose();
            psesHost.StopAsync().Wait();
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

        private VariableDetailsBase[] GetVariables(string scopeName)
        {
            VariableScope scope = Array.Find(
                debugService.GetVariableScopes(0),
                s => s.Name == scopeName);
            return debugService.GetVariables(scope.Id);
        }

        private Task ExecutePowerShellCommand(string command, params string[] args)
        {
            return psesHost.ExecutePSCommandAsync(
                PSCommandHelpers.BuildDotSourceCommandWithArguments(string.Concat('"', command, '"'), args),
                CancellationToken.None);
        }

        private Task ExecuteDebugFile() => ExecutePowerShellCommand(debugScriptFile.FilePath);

        private Task ExecuteVariableScriptFile() => ExecutePowerShellCommand(variableScriptFile.FilePath);

        private void AssertDebuggerPaused()
        {
            DebuggerStoppedEventArgs eventArgs = debuggerStoppedQueue.Take(new CancellationTokenSource(5000).Token);
            Assert.Empty(eventArgs.OriginalEvent.Breakpoints);
        }

        private void AssertDebuggerStopped(
            string scriptPath = "",
            int lineNumber = -1,
            CommandBreakpointDetails commandBreakpointDetails = default)
        {
            DebuggerStoppedEventArgs eventArgs = debuggerStoppedQueue.Take(new CancellationTokenSource(5000).Token);

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
                new[] { CommandBreakpointDetails.Create("Get-Random") }).ConfigureAwait(true);

            Task<IReadOnlyList<int>> executeTask = psesHost.ExecutePSCommandAsync<int>(
                new PSCommand().AddScript("Get-Random -SetSeed 42 -Maximum 100"), CancellationToken.None);

            AssertDebuggerStopped("", 1);
            debugService.Continue();
            Assert.Equal(17, (await executeTask.ConfigureAwait(true))[0]);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync().ConfigureAwait(true);
            Assert.Equal(StackFrameDetails.NoFileScriptPath, stackFrames[0].ScriptPath);

            // NOTE: This assertion will fail if any error occurs. Notably this happens in testing
            // when the assembly path changes and the commands definition file can't be found.
            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.GlobalScopeName);
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
            // The path is intentionally odd (some escaped chars but not all) because we are testing
            // the internal path escaping mechanism - it should escape certains chars ([, ] and space) but
            // it should not escape already escaped chars.
            ScriptFile debugWithParamsFile = GetDebugScript("Debug W&ith Params [Test].ps1");

            BreakpointDetails[] breakpoints = await debugService.SetLineBreakpointsAsync(
                debugWithParamsFile,
                new[] { BreakpointDetails.Create(debugWithParamsFile.FilePath, 3) }).ConfigureAwait(true);

            Assert.Single(breakpoints);
            Assert.Collection(breakpoints, (breakpoint) =>
            {
                // TODO: The drive letter becomes lower cased on Windows for some reason.
                Assert.Equal(debugWithParamsFile.FilePath, breakpoint.Source, ignoreCase: true);
                Assert.Equal(3, breakpoint.LineNumber);
                Assert.True(breakpoint.Verified);
            });

            Task _ = ExecutePowerShellCommand(debugWithParamsFile.FilePath, args);

            AssertDebuggerStopped(debugWithParamsFile.FilePath, 3);

            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$Param1");
            Assert.NotNull(var);
            Assert.Equal("\"Foo\"", var.ValueString);
            Assert.False(var.IsExpandable);

            var = Array.Find(variables, v => v.Name == "$Param2");
            Assert.NotNull(var);
            Assert.True(var.IsExpandable);

            VariableDetailsBase[] childVars = debugService.GetVariables(var.Id);
            // 2 variables plus "Raw View"
            Assert.Equal(3, childVars.Length);
            Assert.Equal("\"Bar\"", childVars[0].ValueString);
            Assert.Equal("\"Baz\"", childVars[1].ValueString);

            var = Array.Find(variables, v => v.Name == "$Force");
            Assert.NotNull(var);
            Assert.Equal("True", var.ValueString);
            Assert.True(var.IsExpandable);

            // NOTE: $args are no longer found in AutoVariables but CommandVariables instead.
            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync().ConfigureAwait(true);
            variables = debugService.GetVariables(stackFrames[0].CommandVariables.Id);
            var = Array.Find(variables, v => v.Name == "$args");
            Assert.NotNull(var);
            Assert.True(var.IsExpandable);

            childVars = debugService.GetVariables(var.Id);
            Assert.Equal(2, childVars.Length);
            Assert.Equal("\"Extra1\"", childVars[0].ValueString);
        }

        [Fact]
        public async Task DebuggerSetsAndClearsFunctionBreakpoints()
        {
            CommandBreakpointDetails[] breakpoints = await debugService.SetCommandBreakpointsAsync(
                new[] {
                    CommandBreakpointDetails.Create("Write-Host"),
                    CommandBreakpointDetails.Create("Get-Date")
                }).ConfigureAwait(true);

            Assert.Equal(2, breakpoints.Length);
            Assert.Equal("Write-Host", breakpoints[0].Name);
            Assert.Equal("Get-Date", breakpoints[1].Name);

            breakpoints = await debugService.SetCommandBreakpointsAsync(
                new[] { CommandBreakpointDetails.Create("Get-Host") }).ConfigureAwait(true);

            Assert.Single(breakpoints);
            Assert.Equal("Get-Host", breakpoints[0].Name);

            breakpoints = await debugService.SetCommandBreakpointsAsync(
                Array.Empty<CommandBreakpointDetails>()).ConfigureAwait(true);

            Assert.Empty(breakpoints);
        }

        [Fact]
        public async Task DebuggerStopsOnFunctionBreakpoints()
        {
            CommandBreakpointDetails[] breakpoints = await debugService.SetCommandBreakpointsAsync(
                new[] { CommandBreakpointDetails.Create("Write-Host") }).ConfigureAwait(true);

            Task _ = ExecuteDebugFile();
            AssertDebuggerStopped(debugScriptFile.FilePath, 6);

            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the function breakpoint broke at Write-Host and $i is 1
            VariableDetailsBase i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal("1", i.ValueString);

            // The function breakpoint should fire the next time through the loop.
            debugService.Continue();
            AssertDebuggerStopped(debugScriptFile.FilePath, 6);

            variables = GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the function breakpoint broke at Write-Host and $i is 1
            i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal("2", i.ValueString);
        }

        [Fact]
        public async Task DebuggerSetsAndClearsLineBreakpoints()
        {
            BreakpointDetails[] breakpoints =
                await debugService.SetLineBreakpointsAsync(
                    debugScriptFile,
                    new[] {
                        BreakpointDetails.Create(debugScriptFile.FilePath, 5),
                        BreakpointDetails.Create(debugScriptFile.FilePath, 10)
                    }).ConfigureAwait(true);

            IReadOnlyList<LineBreakpoint> confirmedBreakpoints = await GetConfirmedBreakpoints(debugScriptFile).ConfigureAwait(true);

            Assert.Equal(2, confirmedBreakpoints.Count);
            Assert.Equal(5, breakpoints[0].LineNumber);
            Assert.Equal(10, breakpoints[1].LineNumber);

            breakpoints = await debugService.SetLineBreakpointsAsync(
                debugScriptFile,
                new[] { BreakpointDetails.Create(debugScriptFile.FilePath, 2) }).ConfigureAwait(true);
            confirmedBreakpoints = await GetConfirmedBreakpoints(debugScriptFile).ConfigureAwait(true);

            Assert.Single(confirmedBreakpoints);
            Assert.Equal(2, breakpoints[0].LineNumber);

            await debugService.SetLineBreakpointsAsync(
                debugScriptFile,
                Array.Empty<BreakpointDetails>()).ConfigureAwait(true);

            IReadOnlyList<LineBreakpoint> remainingBreakpoints = await GetConfirmedBreakpoints(debugScriptFile).ConfigureAwait(true);
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
                }).ConfigureAwait(true);

            Task _ = ExecuteDebugFile();
            AssertDebuggerStopped(debugScriptFile.FilePath, 5);
            debugService.Continue();
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
                }).ConfigureAwait(true);

            Task _ = ExecuteDebugFile();
            AssertDebuggerStopped(debugScriptFile.FilePath, 7);

            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            VariableDetailsBase i = Array.Find(variables, v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal($"{breakpointValue1}", i.ValueString);

            // The conditional breakpoint should not fire again, until the value of
            // i reaches breakpointValue2.
            debugService.Continue();
            AssertDebuggerStopped(debugScriptFile.FilePath, 7);

            variables = GetVariables(VariableContainerDetails.LocalScopeName);

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
                }).ConfigureAwait(true);

            Task _ = ExecuteDebugFile();
            AssertDebuggerStopped(debugScriptFile.FilePath, 6);

            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

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
                new[] { BreakpointDetails.Create(debugScriptFile.FilePath, 6, null, "$i % 2 -eq 0", $"{hitCount}") }).ConfigureAwait(true);

            Task _ = ExecuteDebugFile();
            AssertDebuggerStopped(debugScriptFile.FilePath, 6);

            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

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
            BreakpointDetails[] breakpoints =
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
                    }).ConfigureAwait(true);

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
        public async Task DebuggerFindsParseableButInvalidSimpleBreakpointConditions()
        {
            BreakpointDetails[] breakpoints =
                await debugService.SetLineBreakpointsAsync(
                    debugScriptFile,
                    new[] {
                        BreakpointDetails.Create(debugScriptFile.FilePath, 5, column: null, condition: "$i == 100"),
                        BreakpointDetails.Create(debugScriptFile.FilePath, 7, column: null, condition: "$i > 100")
                    }).ConfigureAwait(true);

            Assert.Equal(2, breakpoints.Length);
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
            IReadOnlyList<LineBreakpoint> confirmedBreakpoints = await GetConfirmedBreakpoints(debugScriptFile).ConfigureAwait(true);
            Assert.Equal(0, confirmedBreakpoints.Count);
            Task _ = ExecuteDebugFile();
            // NOTE: This must be run on a separate thread so the async event handlers can fire.
            await Task.Run(() => debugService.Break()).ConfigureAwait(true);
            AssertDebuggerPaused();
        }

        [Fact]
        public async Task DebuggerRunsCommandsWhileStopped()
        {
            Task _ = ExecuteDebugFile();
            // NOTE: This must be run on a separate thread so the async event handlers can fire.
            await Task.Run(() => debugService.Break()).ConfigureAwait(true);
            AssertDebuggerPaused();

            // Try running a command from outside the pipeline thread
            Task<IReadOnlyList<int>> executeTask = psesHost.ExecutePSCommandAsync<int>(
                new PSCommand().AddScript("Get-Random -SetSeed 42 -Maximum 100"), CancellationToken.None);
            Assert.Equal(17, (await executeTask.ConfigureAwait(true))[0]);
        }

        [Fact]
        public async Task DebuggerVariableStringDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 8) }).ConfigureAwait(true);

            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

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
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 21) }).ConfigureAwait(true);

            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

            // TODO: Add checks for correct value strings as well
            VariableDetailsBase strVar = Array.Find(variables, v => v.Name == "$strVar");
            Assert.NotNull(strVar);
            Assert.False(strVar.IsExpandable);

            VariableDetailsBase objVar = Array.Find(variables, v => v.Name == "$assocArrVar");
            Assert.NotNull(objVar);
            Assert.True(objVar.IsExpandable);

            VariableDetailsBase[] objChildren = debugService.GetVariables(objVar.Id);
            // Two variables plus "Raw View"
            Assert.Equal(3, objChildren.Length);

            VariableDetailsBase arrVar = Array.Find(variables, v => v.Name == "$arrVar");
            Assert.NotNull(arrVar);
            Assert.True(arrVar.IsExpandable);

            VariableDetailsBase[] arrChildren = debugService.GetVariables(arrVar.Id);
            Assert.Equal(5, arrChildren.Length);

            VariableDetailsBase classVar = Array.Find(variables, v => v.Name == "$classVar");
            Assert.NotNull(classVar);
            Assert.True(classVar.IsExpandable);

            VariableDetailsBase[] classChildren = debugService.GetVariables(classVar.Id);
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
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 14) }).ConfigureAwait(true);

            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            VariableScope[] scopes = debugService.GetVariableScopes(0);
            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

            // Test set of a local string variable (not strongly typed)
            const string newStrValue = "\"Goodbye\"";
            VariableScope localScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.LocalScopeName);
            string setStrValue = await debugService.SetVariableAsync(localScope.Id, "$strVar", newStrValue).ConfigureAwait(true);
            Assert.Equal(newStrValue, setStrValue);

            // Test set of script scope int variable (not strongly typed)
            VariableScope scriptScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.ScriptScopeName);
            const string newIntValue = "49";
            const string newIntExpr = "7 * 7";
            string setIntValue = await debugService.SetVariableAsync(scriptScope.Id, "$scriptInt", newIntExpr).ConfigureAwait(true);
            Assert.Equal(newIntValue, setIntValue);

            // Test set of global scope int variable (not strongly typed)
            VariableScope globalScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.GlobalScopeName);
            const string newGlobalIntValue = "4242";
            string setGlobalIntValue = await debugService.SetVariableAsync(globalScope.Id, "$MaximumHistoryCount", newGlobalIntValue).ConfigureAwait(true);
            Assert.Equal(newGlobalIntValue, setGlobalIntValue);

            // The above just tests that the debug service returns the correct new value string.
            // Let's step the debugger and make sure the values got set to the new values.
            await Task.Run(() => debugService.StepOver()).ConfigureAwait(true);
            AssertDebuggerStopped(variableScriptFile.FilePath);

            // Test set of a local string variable (not strongly typed)
            variables = GetVariables(VariableContainerDetails.LocalScopeName);
            VariableDetailsBase strVar = Array.Find(variables, v => v.Name == "$strVar");
            Assert.Equal(newStrValue, strVar.ValueString);

            // Test set of script scope int variable (not strongly typed)
            variables = GetVariables(VariableContainerDetails.ScriptScopeName);
            VariableDetailsBase intVar = Array.Find(variables, v => v.Name == "$scriptInt");
            Assert.Equal(newIntValue, intVar.ValueString);

            // Test set of global scope int variable (not strongly typed)
            variables = GetVariables(VariableContainerDetails.GlobalScopeName);
            VariableDetailsBase intGlobalVar = Array.Find(variables, v => v.Name == "$MaximumHistoryCount");
            Assert.Equal(newGlobalIntValue, intGlobalVar.ValueString);
        }

        [Fact]
        public async Task DebuggerSetsVariablesWithConversion()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 14) }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            VariableScope[] scopes = debugService.GetVariableScopes(0);
            VariableDetailsBase[] variables = GetVariables(VariableContainerDetails.LocalScopeName);

            // Test set of a local string variable (not strongly typed but force conversion)
            const string newStrValue = "\"False\"";
            const string newStrExpr = "$false";
            VariableScope localScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.LocalScopeName);
            string setStrValue = await debugService.SetVariableAsync(localScope.Id, "$strVar2", newStrExpr).ConfigureAwait(true);
            Assert.Equal(newStrValue, setStrValue);

            // Test set of script scope bool variable (strongly typed)
            VariableScope scriptScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.ScriptScopeName);
            const string newBoolValue = "$true";
            const string newBoolExpr = "1";
            string setBoolValue = await debugService.SetVariableAsync(scriptScope.Id, "$scriptBool", newBoolExpr).ConfigureAwait(true);
            Assert.Equal(newBoolValue, setBoolValue);

            // Test set of global scope ActionPreference variable (strongly typed)
            VariableScope globalScope = Array.Find(scopes, s => s.Name == VariableContainerDetails.GlobalScopeName);
            const string newGlobalValue = "Continue";
            const string newGlobalExpr = "'Continue'";
            string setGlobalValue = await debugService.SetVariableAsync(globalScope.Id, "$VerbosePreference", newGlobalExpr).ConfigureAwait(true);
            Assert.Equal(newGlobalValue, setGlobalValue);

            // The above just tests that the debug service returns the correct new value string.
            // Let's step the debugger and make sure the values got set to the new values.
            await Task.Run(() => debugService.StepOver()).ConfigureAwait(true);
            AssertDebuggerStopped(variableScriptFile.FilePath);

            // Test set of a local string variable (not strongly typed but force conversion)
            variables = GetVariables(VariableContainerDetails.LocalScopeName);
            VariableDetailsBase strVar = Array.Find(variables, v => v.Name == "$strVar2");
            Assert.Equal(newStrValue, strVar.ValueString);

            // Test set of script scope bool variable (strongly typed)
            variables = GetVariables(VariableContainerDetails.ScriptScopeName);
            VariableDetailsBase boolVar = Array.Find(variables, v => v.Name == "$scriptBool");
            Assert.Equal(newBoolValue, boolVar.ValueString);

            // Test set of global scope ActionPreference variable (strongly typed)
            variables = GetVariables(VariableContainerDetails.GlobalScopeName);
            VariableDetailsBase globalVar = Array.Find(variables, v => v.Name == "$VerbosePreference");
            Assert.Equal(newGlobalValue, globalVar.ValueString);
        }

        [Fact]
        public async Task DebuggerVariableEnumDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 15) }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync().ConfigureAwait(true);
            VariableDetailsBase[] variables = debugService.GetVariables(stackFrames[0].AutoVariables.Id);

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
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 11) }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync().ConfigureAwait(true);
            VariableDetailsBase[] variables = debugService.GetVariables(stackFrames[0].AutoVariables.Id);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$assocArrVar");
            Assert.NotNull(var);
            Assert.Equal("[Hashtable: 2]", var.ValueString);
            Assert.True(var.IsExpandable);

            VariableDetailsBase[] childVars = debugService.GetVariables(var.Id);
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
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 16) }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync().ConfigureAwait(true);
            VariableDetailsBase[] variables = debugService.GetVariables(stackFrames[0].AutoVariables.Id);

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
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 17) }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync().ConfigureAwait(true);
            VariableDetailsBase[] variables = debugService.GetVariables(stackFrames[0].AutoVariables.Id);

            VariableDetailsBase psObjVar = Array.Find(variables, v => v.Name == "$psObjVar");
            Assert.NotNull(psObjVar);
            Assert.True("@{Age=75; Name=John}".Equals(psObjVar.ValueString) || "@{Name=John; Age=75}".Equals(psObjVar.ValueString));
            Assert.True(psObjVar.IsExpandable);

            IDictionary<string, string> childVars = debugService.GetVariables(psObjVar.Id).ToDictionary(v => v.Name, v => v.ValueString);
            Assert.Equal(2, childVars.Count);
            Assert.Contains("Age", childVars.Keys);
            Assert.Contains("Name", childVars.Keys);
            Assert.Equal("75", childVars["Age"]);
            Assert.Equal("\"John\"", childVars["Name"]);
        }

        [Fact]
        public async Task DebuggerEnumerableShowsRawView()
        {
            CommandBreakpointDetails breakpoint = CommandBreakpointDetails.Create("__BreakDebuggerEnumerableShowsRawView");
            await debugService.SetCommandBreakpointsAsync(new[] { breakpoint }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(commandBreakpointDetails: breakpoint);

            VariableDetailsBase simpleArrayVar = Array.Find(
                GetVariables(VariableContainerDetails.ScriptScopeName),
                v => v.Name == "$simpleArray");
            Assert.NotNull(simpleArrayVar);
            VariableDetailsBase rawDetailsView = Array.Find(
                simpleArrayVar.GetChildren(NullLogger.Instance),
                v => v.Name == "Raw View");
            Assert.NotNull(rawDetailsView);
            Assert.Empty(rawDetailsView.Type);
            Assert.Empty(rawDetailsView.ValueString);
            VariableDetailsBase[] rawViewChildren = rawDetailsView.GetChildren(NullLogger.Instance);
            Assert.Equal(7, rawViewChildren.Length);
            Assert.Equal("Length", rawViewChildren[0].Name);
            Assert.Equal("4", rawViewChildren[0].ValueString);
            Assert.Equal("LongLength", rawViewChildren[1].Name);
            Assert.Equal("4", rawViewChildren[1].ValueString);
            Assert.Equal("Rank", rawViewChildren[2].Name);
            Assert.Equal("1", rawViewChildren[2].ValueString);
            Assert.Equal("SyncRoot", rawViewChildren[3].Name);
            Assert.Equal("IsReadOnly", rawViewChildren[4].Name);
            Assert.Equal("$false", rawViewChildren[4].ValueString);
            Assert.Equal("IsFixedSize", rawViewChildren[5].Name);
            Assert.Equal("$true", rawViewChildren[5].ValueString);
            Assert.Equal("IsSynchronized", rawViewChildren[6].Name);
            Assert.Equal("$false", rawViewChildren[6].ValueString);
        }

        [Fact]
        public async Task DebuggerDictionaryShowsRawView()
        {
            CommandBreakpointDetails breakpoint = CommandBreakpointDetails.Create("__BreakDebuggerDictionaryShowsRawView");
            await debugService.SetCommandBreakpointsAsync(new[] { breakpoint }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(commandBreakpointDetails: breakpoint);

            VariableDetailsBase simpleDictionaryVar = Array.Find(
                GetVariables(VariableContainerDetails.ScriptScopeName),
                v => v.Name == "$simpleDictionary");
            Assert.NotNull(simpleDictionaryVar);
            VariableDetailsBase rawDetailsView = Array.Find(
                simpleDictionaryVar.GetChildren(NullLogger.Instance),
                v => v.Name == "Raw View");
            Assert.NotNull(rawDetailsView);
            Assert.Empty(rawDetailsView.Type);
            Assert.Empty(rawDetailsView.ValueString);
            VariableDetailsBase[] rawViewChildren = rawDetailsView.GetChildren(NullLogger.Instance);
            Assert.Equal(7, rawViewChildren.Length);
            Assert.Equal("IsReadOnly", rawViewChildren[0].Name);
            Assert.Equal("$false", rawViewChildren[0].ValueString);
            Assert.Equal("IsFixedSize", rawViewChildren[1].Name);
            Assert.Equal("$false", rawViewChildren[1].ValueString);
            Assert.Equal("IsSynchronized", rawViewChildren[2].Name);
            Assert.Equal("$false", rawViewChildren[2].ValueString);
            Assert.Equal("Keys", rawViewChildren[3].Name);
            Assert.Equal("Values", rawViewChildren[4].Name);
            Assert.Equal("[ValueCollection: 4]", rawViewChildren[4].ValueString);
            Assert.Equal("SyncRoot", rawViewChildren[5].Name);
            Assert.Equal("Count", rawViewChildren[6].Name);
            Assert.Equal("4", rawViewChildren[6].ValueString);
        }

        [Fact]
        public async Task DebuggerDerivedDictionaryPropertyInRawView()
        {
            CommandBreakpointDetails breakpoint = CommandBreakpointDetails.Create("__BreakDebuggerDerivedDictionaryPropertyInRawView");
            await debugService.SetCommandBreakpointsAsync(new[] { breakpoint }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(commandBreakpointDetails: breakpoint);

            VariableDetailsBase sortedDictionaryVar = Array.Find(
                GetVariables(VariableContainerDetails.ScriptScopeName),
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
            Assert.Equal(4, rawViewChildren.Length);
            Assert.NotNull(Array.Find(rawViewChildren, v => v.Name == "Comparer"));
        }

        [Fact]
        public async Task DebuggerVariablePSCustomObjectDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 18) }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync().ConfigureAwait(true);
            VariableDetailsBase[] variables = debugService.GetVariables(stackFrames[0].AutoVariables.Id);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$psCustomObjVar");
            Assert.NotNull(var);
            Assert.Equal("@{Name=Paul; Age=73}", var.ValueString);
            Assert.True(var.IsExpandable);

            VariableDetailsBase[] childVars = debugService.GetVariables(var.Id);
            Assert.Equal(2, childVars.Length);
            Assert.Equal("Name", childVars[0].Name);
            Assert.Equal("\"Paul\"", childVars[0].ValueString);
            Assert.Equal("Age", childVars[1].Name);
            Assert.Equal("73", childVars[1].ValueString);
        }

        // Verifies fix for issue #86, $proc = Get-Process foo displays just the ETS property set
        // and not all process properties.
        [Fact(Skip = "Length of child vars is wrong now")]
        public async Task DebuggerVariableProcessObjDisplaysCorrectly()
        {
            await debugService.SetLineBreakpointsAsync(
                variableScriptFile,
                new[] { BreakpointDetails.Create(variableScriptFile.FilePath, 19) }).ConfigureAwait(true);

            // Execute the script and wait for the breakpoint to be hit
            Task _ = ExecuteVariableScriptFile();
            AssertDebuggerStopped(variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = await debugService.GetStackFramesAsync().ConfigureAwait(true);
            VariableDetailsBase[] variables = debugService.GetVariables(stackFrames[0].AutoVariables.Id);

            VariableDetailsBase var = Array.Find(variables, v => v.Name == "$procVar");
            Assert.NotNull(var);
            Assert.StartsWith("System.Diagnostics.Process", var.ValueString);
            Assert.True(var.IsExpandable);

            VariableDetailsBase[] childVars = debugService.GetVariables(var.Id);
            Assert.Equal(53, childVars.Length);
        }
    }
}
