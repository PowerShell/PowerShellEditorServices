//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Debugging;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Debugging
{
    public class DebugServiceTests : IDisposable
    {
        private Workspace workspace;
        private DebugService debugService;
        private ScriptFile debugScriptFile;
        private ScriptFile variableScriptFile;
        private PowerShellContext powerShellContext;
        private SynchronizationContext runnerContext;

        private AsyncQueue<DebuggerStoppedEventArgs> debuggerStoppedQueue =
            new AsyncQueue<DebuggerStoppedEventArgs>();
        private AsyncQueue<SessionStateChangedEventArgs> sessionStateQueue =
            new AsyncQueue<SessionStateChangedEventArgs>();

        public DebugServiceTests()
        {
            this.powerShellContext = new PowerShellContext();
            this.powerShellContext.SessionStateChanged += powerShellContext_SessionStateChanged;

            this.workspace = new Workspace(this.powerShellContext.LocalPowerShellVersion.Version);

            // Load the test debug file
            this.debugScriptFile =
                this.workspace.GetFile(
                    @"..\..\..\..\PowerShellEditorServices.Test.Shared\Debugging\DebugTest.ps1");

            this.variableScriptFile =
                this.workspace.GetFile(
                    @"..\..\..\..\PowerShellEditorServices.Test.Shared\Debugging\VariableTest.ps1");

            this.debugService = new DebugService(this.powerShellContext);
            this.debugService.DebuggerStopped += debugService_DebuggerStopped;
            this.debugService.BreakpointUpdated += debugService_BreakpointUpdated;
            this.runnerContext = SynchronizationContext.Current;

            // Load the test debug file
            this.debugScriptFile =
                this.workspace.GetFile(
                    @"..\..\..\..\PowerShellEditorServices.Test.Shared\Debugging\DebugTest.ps1");
        }

        async void powerShellContext_SessionStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            // Skip all transitions except those back to 'Ready'
            if (e.NewSessionState == PowerShellContextState.Ready)
            {
                await this.sessionStateQueue.EnqueueAsync(e);
            }
        }

        void debugService_BreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            // TODO: Needed?
        }

        async void debugService_DebuggerStopped(object sender, DebuggerStoppedEventArgs e)
        {
            await this.debuggerStoppedQueue.EnqueueAsync(e);
        }

        public void Dispose()
        {
            this.powerShellContext.Dispose();
        }

        public static IEnumerable<object[]> DebuggerAcceptsScriptArgsTestData
        {
            get
            {
                var data = new[]
                {
                    new[] { new []{ "Foo -Param2 @('Bar','Baz') -Force Extra1" }},
                    new[] { new []{ "Foo", "-Param2", "@('Bar','Baz')", "-Force", "Extra1" }},
                };

                return data;
            }
        }

        [Theory]
        [MemberData("DebuggerAcceptsScriptArgsTestData")]
        public async Task DebuggerAcceptsScriptArgs(string[] args)
        {
            // The path is intentionally odd (some escaped chars but not all) because we are testing
            // the internal path escaping mechanism - it should escape certains chars ([, ] and space) but
            // it should not escape already escaped chars.
            ScriptFile debugWithParamsFile =
                this.workspace.GetFile(
                    @"..\..\..\..\PowerShellEditorServices.Test.Shared\Debugging\Debug` With Params `[Test].ps1");

            await this.debugService.SetLineBreakpoints(
                debugWithParamsFile,
                new[] { BreakpointDetails.Create("", 3) });

            string arguments = string.Join(" ", args);

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptAtPath(
                    debugWithParamsFile.FilePath, arguments);

            await this.AssertDebuggerStopped(debugWithParamsFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            var var = variables.FirstOrDefault(v => v.Name == "$Param1");
            Assert.NotNull(var);
            Assert.Equal("\"Foo\"", var.ValueString);
            Assert.False(var.IsExpandable);

            var = variables.FirstOrDefault(v => v.Name == "$Param2");
            Assert.NotNull(var);
            Assert.True(var.IsExpandable);
            
            var childVars = debugService.GetVariables(var.Id);
            Assert.Equal(9, childVars.Length);
            Assert.Equal("\"Bar\"", childVars[0].ValueString);
            Assert.Equal("\"Baz\"", childVars[1].ValueString);

            var = variables.FirstOrDefault(v => v.Name == "$Force");
            Assert.NotNull(var);
            Assert.Equal("True", var.ValueString);
            Assert.True(var.IsExpandable);

            var = variables.FirstOrDefault(v => v.Name == "$args");
            Assert.NotNull(var);
            Assert.True(var.IsExpandable);

            childVars = debugService.GetVariables(var.Id);
            Assert.Equal(8, childVars.Length);
            Assert.Equal("\"Extra1\"", childVars[0].ValueString);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        [Fact]
        public async Task DebuggerSetsAndClearsFunctionBreakpoints()
        {
            CommandBreakpointDetails[] breakpoints =
                await this.debugService.SetCommandBreakpoints(
                    new[] {
                        CommandBreakpointDetails.Create("Write-Host"),
                        CommandBreakpointDetails.Create("Get-Date")
                    });

            Assert.Equal(2, breakpoints.Length);
            Assert.Equal("Write-Host", breakpoints[0].Name);
            Assert.Equal("Get-Date", breakpoints[1].Name);

            breakpoints =
                await this.debugService.SetCommandBreakpoints(
                    new[] { CommandBreakpointDetails.Create("Get-Host") });

            Assert.Equal(1, breakpoints.Length);
            Assert.Equal("Get-Host", breakpoints[0].Name);

            breakpoints =
                await this.debugService.SetCommandBreakpoints(
                    new CommandBreakpointDetails[] {});

            Assert.Equal(0, breakpoints.Length);
        }

        [Fact]
        public async Task DebuggerStopsOnFunctionBreakpoints()
        {
            CommandBreakpointDetails[] breakpoints =
                await this.debugService.SetCommandBreakpoints(
                    new[] {
                        CommandBreakpointDetails.Create("Write-Host")
                    });

            await this.AssertStateChange(PowerShellContextState.Ready);

            Task executeTask =
                this.powerShellContext.ExecuteScriptAtPath(
                    this.debugScriptFile.FilePath);

            // Wait for function breakpoint to hit
            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 6);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();
            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // Verify the function breakpoint broke at Write-Host and $i is 1
            var i = variables.FirstOrDefault(v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal("1", i.ValueString);

            // The function breakpoint should fire the next time through the loop.
            this.debugService.Continue();
            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 6);

            stackFrames = debugService.GetStackFrames();
            variables = debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // Verify the function breakpoint broke at Write-Host and $i is 1
            i = variables.FirstOrDefault(v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal("2", i.ValueString);

            // Abort script execution early and wait for completion
            this.debugService.Abort();
            await executeTask;
        }

        [Fact]
        public async Task DebuggerSetsAndClearsLineBreakpoints()
        {
            BreakpointDetails[] breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] {
                        BreakpointDetails.Create("", 5),
                        BreakpointDetails.Create("", 10)
                    });

            Assert.Equal(2, breakpoints.Length);
            Assert.Equal(5, breakpoints[0].LineNumber);
            Assert.Equal(10, breakpoints[1].LineNumber);

            breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] { BreakpointDetails.Create("", 2) });

            Assert.Equal(1, breakpoints.Length);
            Assert.Equal(2, breakpoints[0].LineNumber);

            breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] { BreakpointDetails.Create("", 0) });

            Assert.Equal(0, breakpoints.Length);
        }

        [Fact]
        public async Task DebuggerStopsOnLineBreakpoints()
        {
            BreakpointDetails[] breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] {
                        BreakpointDetails.Create("", 5),
                        BreakpointDetails.Create("", 7)
                    });

            await this.AssertStateChange(PowerShellContextState.Ready);

            Task executeTask =
                this.powerShellContext.ExecuteScriptAtPath(
                    this.debugScriptFile.FilePath);

            // Wait for a couple breakpoints
            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 5);
            this.debugService.Continue();

            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 7);

            // Abort script execution early and wait for completion
            this.debugService.Abort();
            await executeTask;
        }

        [Fact]
        public async Task DebuggerStopsOnConditionalBreakpoints()
        {
            const int breakpointValue1 = 10;
            const int breakpointValue2 = 20;

            BreakpointDetails[] breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] {
                        BreakpointDetails.Create("", 7, null, $"$i -eq {breakpointValue1} -or $i -eq {breakpointValue2}"),
                    });

            await this.AssertStateChange(PowerShellContextState.Ready);

            Task executeTask =
                this.powerShellContext.ExecuteScriptAtPath(
                    this.debugScriptFile.FilePath);

            // Wait for conditional breakpoint to hit
            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 7);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();
            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            var i = variables.FirstOrDefault(v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal($"{breakpointValue1}", i.ValueString);

            // The conditional breakpoint should not fire again, until the value of
            // i reaches breakpointValue2.
            this.debugService.Continue();
            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 7);

            stackFrames = debugService.GetStackFrames();
            variables = debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            i = variables.FirstOrDefault(v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal($"{breakpointValue2}", i.ValueString);

            // Abort script execution early and wait for completion
            this.debugService.Abort();
            await executeTask;
        }

        [Fact]
        public async Task DebuggerStopsOnHitConditionBreakpoint()
        {
            const int hitCount = 5;

            BreakpointDetails[] breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] {
                        BreakpointDetails.Create("", 6, null, null, $"{hitCount}"),
                    });

            await this.AssertStateChange(PowerShellContextState.Ready);

            Task executeTask =
                this.powerShellContext.ExecuteScriptAtPath(
                    this.debugScriptFile.FilePath);

            // Wait for conditional breakpoint to hit
            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 6);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();
            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            var i = variables.FirstOrDefault(v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            Assert.Equal($"{hitCount}", i.ValueString);

            // Abort script execution early and wait for completion
            this.debugService.Abort();
            await executeTask;
        }

        [Fact]
        public async Task DebuggerStopsOnConditionalAndHitConditionBreakpoint()
        {
            const int hitCount = 5;

            BreakpointDetails[] breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] {
                        BreakpointDetails.Create("", 6, null, $"$i % 2 -eq 0", $"{hitCount}"),
                    });

            await this.AssertStateChange(PowerShellContextState.Ready);

            Task executeTask =
                this.powerShellContext.ExecuteScriptAtPath(
                    this.debugScriptFile.FilePath);

            // Wait for conditional breakpoint to hit
            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 6);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();
            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // Verify the breakpoint only broke at the condition ie. $i -eq breakpointValue1
            var i = variables.FirstOrDefault(v => v.Name == "$i");
            Assert.NotNull(i);
            Assert.False(i.IsExpandable);
            // Condition is even numbers ($i starting at 1) should end up on 10 with a hit count of 5.
            Assert.Equal("10", i.ValueString);

            // Abort script execution early and wait for completion
            this.debugService.Abort();
            await executeTask;
        }

        [Fact]
        public async Task DebuggerProvidesMessageForInvalidConditionalBreakpoint()
        {
            BreakpointDetails[] breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] {
                        BreakpointDetails.Create("", 5),
                        BreakpointDetails.Create("", 10, column: null, condition: "$i -ez 100")
                    });

            Assert.Equal(2, breakpoints.Length);
            Assert.Equal(5, breakpoints[0].LineNumber);
            Assert.True(breakpoints[0].Verified);
            Assert.Null(breakpoints[0].Message);

            Assert.Equal(10, breakpoints[1].LineNumber);
            Assert.False(breakpoints[1].Verified);
            Assert.NotNull(breakpoints[1].Message);
            Assert.Contains("Unexpected token '-ez'", breakpoints[1].Message);
        }

        [Fact]
        public async Task DebuggerFindsParseableButInvalidSimpleBreakpointConditions()
        {
            BreakpointDetails[] breakpoints =
                await this.debugService.SetLineBreakpoints(
                    this.debugScriptFile,
                    new[] {
                        BreakpointDetails.Create("", 5, column: null, condition: "$i == 100"),
                        BreakpointDetails.Create("", 7, column: null, condition: "$i > 100")
                    });

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
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.debugScriptFile.FilePath);

            // Break execution and wait for the debugger to stop
            this.debugService.Break();

            // File path is an empty string when paused while running
            await this.AssertDebuggerStopped(string.Empty); 
            await this.AssertStateChange(
                PowerShellContextState.Ready,
                PowerShellExecutionResult.Stopped);

            // Abort execution and wait for the debugger to exit
            this.debugService.Abort();
            await this.AssertStateChange(
                PowerShellContextState.Ready,
                PowerShellExecutionResult.Completed);
        }

        [Fact]
        public async Task DebuggerRunsCommandsWhileStopped()
        {
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.debugScriptFile.FilePath);

            // Break execution and wait for the debugger to stop
            this.debugService.Break();
            await this.AssertStateChange(
                PowerShellContextState.Ready,
                PowerShellExecutionResult.Stopped);

            // Try running a command from outside the pipeline thread
            await this.powerShellContext.ExecuteScriptString("Get-Command Get-Process");

            // Abort execution and wait for the debugger to exit
            this.debugService.Abort();
            await this.AssertStateChange(
                PowerShellContextState.Ready,
                PowerShellExecutionResult.Completed);
        }

        [Fact]
        public async Task DebuggerVariableStringDisplaysCorrectly()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 18) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            var var = variables.FirstOrDefault(v => v.Name == "$strVar");
            Assert.NotNull(var);
            Assert.Equal("\"Hello\"", var.ValueString);
            Assert.False(var.IsExpandable);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        [Fact]
        public async Task DebuggerGetsVariables()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 14) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables = 
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // TODO: Add checks for correct value strings as well

            var strVar = variables.FirstOrDefault(v => v.Name == "$strVar");
            Assert.NotNull(strVar);
            Assert.False(strVar.IsExpandable);

            var objVar = variables.FirstOrDefault(v => v.Name == "$assocArrVar");
            Assert.NotNull(objVar);
            Assert.True(objVar.IsExpandable);

            var objChildren = debugService.GetVariables(objVar.Id);
            Assert.Equal(9, objChildren.Length);

            var arrVar = variables.FirstOrDefault(v => v.Name == "$arrVar");
            Assert.NotNull(arrVar);
            Assert.True(arrVar.IsExpandable);

            var arrChildren = debugService.GetVariables(arrVar.Id);
            Assert.Equal(11, arrChildren.Length);

            var classVar = variables.FirstOrDefault(v => v.Name == "$classVar");
            Assert.NotNull(classVar);
            Assert.True(classVar.IsExpandable);

            var classChildren = debugService.GetVariables(classVar.Id);
            Assert.Equal(2, classChildren.Length);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        [Fact]
        public async Task DebuggerSetsVariablesNoConversion()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 14) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // Test set of a local string variable (not strongly typed)
            string newStrValue = "\"Goodbye\"";
            string setStrValue = await debugService.SetVariable(stackFrames[0].LocalVariables.Id, "$strVar", newStrValue);
            Assert.Equal(newStrValue, setStrValue);

            VariableScope[] scopes = this.debugService.GetVariableScopes(0);
            
            // Test set of script scope int variable (not strongly typed)
            VariableScope scriptScope = scopes.FirstOrDefault(s => s.Name == VariableContainerDetails.ScriptScopeName);
            string newIntValue = "49";
            string newIntExpr = "7 * 7";
            string setIntValue = await debugService.SetVariable(scriptScope.Id, "$scriptInt", newIntExpr);
            Assert.Equal(newIntValue, setIntValue);

            // Test set of global scope int variable (not strongly typed)
            VariableScope globalScope = scopes.FirstOrDefault(s => s.Name == VariableContainerDetails.GlobalScopeName);
            string newGlobalIntValue = "4242";
            string setGlobalIntValue = await debugService.SetVariable(globalScope.Id, "$MaximumAliasCount", newGlobalIntValue);
            Assert.Equal(newGlobalIntValue, setGlobalIntValue);

            // The above just tests that the debug service returns the correct new value string.
            // Let's step the debugger and make sure the values got set to the new values.
            this.debugService.StepOver();
            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            stackFrames = debugService.GetStackFrames();

            // Test set of a local string variable (not strongly typed)
            variables = debugService.GetVariables(stackFrames[0].LocalVariables.Id);
            var strVar = variables.FirstOrDefault(v => v.Name == "$strVar");
            Assert.Equal(newStrValue, strVar.ValueString);

            scopes = this.debugService.GetVariableScopes(0);

            // Test set of script scope int variable (not strongly typed)
            scriptScope = scopes.FirstOrDefault(s => s.Name == VariableContainerDetails.ScriptScopeName);
            variables = debugService.GetVariables(scriptScope.Id);
            var intVar = variables.FirstOrDefault(v => v.Name == "$scriptInt");
            Assert.Equal(newIntValue, intVar.ValueString);

            // Test set of global scope int variable (not strongly typed)
            globalScope = scopes.FirstOrDefault(s => s.Name == VariableContainerDetails.GlobalScopeName);
            variables = debugService.GetVariables(globalScope.Id);
            var intGlobalVar = variables.FirstOrDefault(v => v.Name == "$MaximumAliasCount");
            Assert.Equal(newGlobalIntValue, intGlobalVar.ValueString);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        [Fact]
        public async Task DebuggerSetsVariablesWithConversion()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 14) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // Test set of a local string variable (not strongly typed but force conversion)
            string newStrValue = "\"False\"";
            string newStrExpr = "$false";
            string setStrValue = await debugService.SetVariable(stackFrames[0].LocalVariables.Id, "$strVar2", newStrExpr);
            Assert.Equal(newStrValue, setStrValue);

            VariableScope[] scopes = this.debugService.GetVariableScopes(0);

            // Test set of script scope bool variable (strongly typed)
            VariableScope scriptScope = scopes.FirstOrDefault(s => s.Name == VariableContainerDetails.ScriptScopeName);
            string newBoolValue = "$true";
            string newBoolExpr = "1";
            string setBoolValue = await debugService.SetVariable(scriptScope.Id, "$scriptBool", newBoolExpr);
            Assert.Equal(newBoolValue, setBoolValue);

            // Test set of global scope ActionPreference variable (strongly typed)
            VariableScope globalScope = scopes.FirstOrDefault(s => s.Name == VariableContainerDetails.GlobalScopeName);
            string newGlobalValue = "Continue";
            string newGlobalExpr = "'Continue'";
            string setGlobalValue = await debugService.SetVariable(globalScope.Id, "$VerbosePreference", newGlobalExpr);
            Assert.Equal(newGlobalValue, setGlobalValue);

            // The above just tests that the debug service returns the correct new value string.
            // Let's step the debugger and make sure the values got set to the new values.
            this.debugService.StepOver();
            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            stackFrames = debugService.GetStackFrames();

            // Test set of a local string variable (not strongly typed but force conversion)
            variables = debugService.GetVariables(stackFrames[0].LocalVariables.Id);
            var strVar = variables.FirstOrDefault(v => v.Name == "$strVar2");
            Assert.Equal(newStrValue, strVar.ValueString);

            scopes = this.debugService.GetVariableScopes(0);

            // Test set of script scope bool variable (strongly typed)
            scriptScope = scopes.FirstOrDefault(s => s.Name == VariableContainerDetails.ScriptScopeName);
            variables = debugService.GetVariables(scriptScope.Id);
            var boolVar = variables.FirstOrDefault(v => v.Name == "$scriptBool");
            Assert.Equal(newBoolValue, boolVar.ValueString);

            // Test set of global scope ActionPreference variable (strongly typed)
            globalScope = scopes.FirstOrDefault(s => s.Name == VariableContainerDetails.GlobalScopeName);
            variables = debugService.GetVariables(globalScope.Id);
            var globalVar = variables.FirstOrDefault(v => v.Name == "$VerbosePreference");
            Assert.Equal(newGlobalValue, globalVar.ValueString);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        [Fact]
        public async Task DebuggerVariableEnumDisplaysCorrectly()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 18) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            var var = variables.FirstOrDefault(v => v.Name == "$enumVar");
            Assert.NotNull(var);
            Assert.Equal("Continue", var.ValueString);
            Assert.False(var.IsExpandable);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        [Fact]
        public async Task DebuggerVariableHashtableDisplaysCorrectly()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 18) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            var var = variables.FirstOrDefault(v => v.Name == "$assocArrVar");
            Assert.NotNull(var);
            Assert.Equal("[Hashtable: 2]", var.ValueString);
            Assert.True(var.IsExpandable);

            var childVars = debugService.GetVariables(var.Id);
            Assert.Equal(9, childVars.Length);
            Assert.Equal("[0]", childVars[0].Name);
            Assert.Equal("[secondChild, 42]", childVars[0].ValueString);
            Assert.Equal("[1]", childVars[1].Name);
            Assert.Equal("[firstChild, \"Child\"]", childVars[1].ValueString);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        [Fact]
        public async Task DebuggerVariablePSObjectDisplaysCorrectly()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 18) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            var var = variables.FirstOrDefault(v => v.Name == "$psObjVar");
            Assert.NotNull(var);
            Assert.Equal("@{Age=75; Name=John}", var.ValueString);
            Assert.True(var.IsExpandable);

            var childVars = debugService.GetVariables(var.Id);
            Assert.Equal(2, childVars.Length);
            Assert.Equal("Age", childVars[0].Name);
            Assert.Equal("75", childVars[0].ValueString);
            Assert.Equal("Name", childVars[1].Name);
            Assert.Equal("\"John\"", childVars[1].ValueString);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        [Fact]
        public async Task DebuggerVariablePSCustomObjectDisplaysCorrectly()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 18) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            var var = variables.FirstOrDefault(v => v.Name == "$psCustomObjVar");
            Assert.NotNull(var);
            Assert.Equal("@{Name=Paul; Age=73}", var.ValueString);
            Assert.True(var.IsExpandable);

            var childVars = debugService.GetVariables(var.Id);
            Assert.Equal(2, childVars.Length);
            Assert.Equal("Name", childVars[0].Name);
            Assert.Equal("\"Paul\"", childVars[0].ValueString);
            Assert.Equal("Age", childVars[1].Name);
            Assert.Equal("73", childVars[1].ValueString);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        // Verifies fix for issue #86, $proc = Get-Process foo displays just the
        // ETS property set and not all process properties.
        [Fact]
        public async Task DebuggerVariableProcessObjDisplaysCorrectly()
        {
            await this.debugService.SetLineBreakpoints(
                this.variableScriptFile,
                new[] { BreakpointDetails.Create("", 18) });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    this.variableScriptFile.FilePath);

            await this.AssertDebuggerStopped(this.variableScriptFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables =
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            var var = variables.FirstOrDefault(v => v.Name == "$procVar");
            Assert.NotNull(var);
            Assert.Equal("System.Diagnostics.Process (System)", var.ValueString);
            Assert.True(var.IsExpandable);

            var childVars = debugService.GetVariables(var.Id);
            Assert.Equal(52, childVars.Length);

            // Abort execution of the script
            this.powerShellContext.AbortExecution();
        }

        public async Task AssertDebuggerStopped(
            string scriptPath,
            int lineNumber = -1)
        {
            SynchronizationContext syncContext = SynchronizationContext.Current;

            DebuggerStoppedEventArgs eventArgs =
                await this.debuggerStoppedQueue.DequeueAsync();

            Assert.Equal(scriptPath, eventArgs.ScriptPath);
            if (lineNumber > -1)
            {
                Assert.Equal(lineNumber, eventArgs.LineNumber);
            }
        }

        private async Task AssertStateChange(
            PowerShellContextState expectedState,
            PowerShellExecutionResult expectedResult = PowerShellExecutionResult.Completed)
        {
            SessionStateChangedEventArgs newState =
                await this.sessionStateQueue.DequeueAsync();

            Assert.Equal(expectedState, newState.NewSessionState);
            Assert.Equal(expectedResult, newState.ExecutionResult);
        }
    }
}


