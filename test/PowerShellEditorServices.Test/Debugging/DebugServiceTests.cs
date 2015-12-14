//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Nito.AsyncEx;
using System;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.PowerShell.EditorServices.Test.Debugging
{
    public class DebugServiceTests : IDisposable
    {
        private Workspace workspace;
        private DebugService debugService;
        private ScriptFile debugScriptFile;
        private PowerShellContext powerShellContext;
        private SynchronizationContext runnerContext;

        private AsyncProducerConsumerQueue<DebuggerStopEventArgs> debuggerStoppedQueue =
            new AsyncProducerConsumerQueue<DebuggerStopEventArgs>();
        private AsyncProducerConsumerQueue<SessionStateChangedEventArgs> sessionStateQueue =
            new AsyncProducerConsumerQueue<SessionStateChangedEventArgs>();

        public DebugServiceTests()
        {
            this.workspace = new Workspace();

            // Load the test debug file
            this.debugScriptFile =
                this.workspace.GetFile(
                    @"..\..\..\PowerShellEditorServices.Test.Shared\Debugging\DebugTest.ps1");

            this.powerShellContext = new PowerShellContext();
            this.powerShellContext.SessionStateChanged += powerShellContext_SessionStateChanged;

            this.debugService = new DebugService(this.powerShellContext);
            this.debugService.DebuggerStopped += debugService_DebuggerStopped;
            this.debugService.BreakpointUpdated += debugService_BreakpointUpdated;
            this.runnerContext = SynchronizationContext.Current;
        }

        void powerShellContext_SessionStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            // Skip all transitions except those back to 'Ready'
            if (e.NewSessionState == PowerShellContextState.Ready)
            {
                this.sessionStateQueue.Enqueue(e);
            }
        }

        void debugService_BreakpointUpdated(object sender, BreakpointUpdatedEventArgs e)
        {
            // TODO: Needed?
        }

        void debugService_DebuggerStopped(object sender, DebuggerStopEventArgs e)
        {
            this.debuggerStoppedQueue.Enqueue(e);
        }

        public void Dispose()
        {
            this.powerShellContext.Dispose();
        }

        [Fact]
        public async Task DebuggerSetsAndClearsBreakpoints()
        {
            BreakpointDetails[] breakpoints =
                await this.debugService.SetBreakpoints(
                    this.debugScriptFile, 
                    new int[] { 5, 9 });

            Assert.Equal(2, breakpoints.Length);
            Assert.Equal(5, breakpoints[0].LineNumber);
            Assert.Equal(9, breakpoints[1].LineNumber);

            breakpoints =
                await this.debugService.SetBreakpoints(
                    this.debugScriptFile, 
                    new int[] { 2 });

            Assert.Equal(1, breakpoints.Length);
            Assert.Equal(2, breakpoints[0].LineNumber);

            breakpoints =
                await this.debugService.SetBreakpoints(
                    this.debugScriptFile, 
                    new int[0]);

            Assert.Equal(0, breakpoints.Length);
        }

        [Fact]
        public async Task DebuggerStopsOnBreakpoints()
        {
            BreakpointDetails[] breakpoints =
                await this.debugService.SetBreakpoints(
                    this.debugScriptFile, 
                    new int[] { 5, 9 });
            await this.AssertStateChange(PowerShellContextState.Ready);

            Task executeTask =
                this.powerShellContext.ExecuteScriptAtPath(
                    this.debugScriptFile.FilePath);

            // Wait for a couple breakpoints
            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 5);
            this.debugService.Continue();

            await this.AssertDebuggerStopped(this.debugScriptFile.FilePath, 9);

            // Abort script execution early and wait for completion
            this.debugService.Abort();
            await executeTask;
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
                PowerShellExecutionResult.Aborted);
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
                PowerShellExecutionResult.Aborted);
        }

        [Fact]
        public async Task DebuggerGetsVariables()
        {
            ScriptFile variablesFile =
                this.workspace.GetFile(
                    @"..\..\..\PowerShellEditorServices.Test.Shared\Debugging\VariableTest.ps1");

            await this.debugService.SetBreakpoints(
                variablesFile, 
                new int[] { 14 });

            // Execute the script and wait for the breakpoint to be hit
            Task executeTask =
                this.powerShellContext.ExecuteScriptString(
                    variablesFile.FilePath);

            await this.AssertDebuggerStopped(variablesFile.FilePath);

            StackFrameDetails[] stackFrames = debugService.GetStackFrames();

            VariableDetailsBase[] variables = 
                debugService.GetVariables(stackFrames[0].LocalVariables.Id);

            // TODO: Add checks for correct value strings as well

            var strVar = variables.FirstOrDefault(v => v.Name == "$strVar");
            Assert.NotNull(strVar);
            Assert.False(strVar.IsExpandable);

            var objVar = variables.FirstOrDefault(v => v.Name == "$objVar");
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

        public async Task AssertDebuggerStopped(
            string scriptPath,
            int lineNumber = -1)
        {
            SynchronizationContext syncContext = SynchronizationContext.Current;

            DebuggerStopEventArgs eventArgs =
                await this.debuggerStoppedQueue.DequeueAsync();

            Assert.Equal(scriptPath, eventArgs.InvocationInfo.ScriptName);
            if (lineNumber > -1)
            {
                Assert.Equal(lineNumber, eventArgs.InvocationInfo.ScriptLineNumber);
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


