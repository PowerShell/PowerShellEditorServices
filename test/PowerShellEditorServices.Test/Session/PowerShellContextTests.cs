//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class PowerShellContextTests : IDisposable
    {
        private PowerShellContext powerShellContext;
        private AsyncProducerConsumerQueue<SessionStateChangedEventArgs> stateChangeQueue;

        private const string DebugTestFilePath =
            @"..\..\..\PowerShellEditorServices.Test.Shared\Debugging\DebugTest.ps1";

        public PowerShellContextTests()
        {
            this.powerShellContext = new PowerShellContext();
            this.powerShellContext.SessionStateChanged += OnSessionStateChanged;
            this.stateChangeQueue = new AsyncProducerConsumerQueue<SessionStateChangedEventArgs>();
        }

        public void Dispose()
        {
            this.powerShellContext.Dispose();
            this.powerShellContext = null;
        }

        [Fact]
        public async Task CanExecutePSCommand()
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddScript("$a = \"foo\"; $a");

            var executeTask =
                this.powerShellContext.ExecuteCommand<string>(psCommand);

            await this.AssertStateChange(PowerShellContextState.Running);
            await this.AssertStateChange(PowerShellContextState.Ready);

            var result = await executeTask;
            Assert.Equal("foo", result.First());
        }

        [Fact]
        public async Task CanQueueParallelRunspaceRequests()
        {
            // Concurrently initiate 4 requests in the session
            this.powerShellContext.ExecuteScriptString("$x = 100");
            Task<RunspaceHandle> handleTask = this.powerShellContext.GetRunspaceHandle();
            this.powerShellContext.ExecuteScriptString("$x += 200");
            this.powerShellContext.ExecuteScriptString("$x = $x / 100");

            PSCommand psCommand = new PSCommand();
            psCommand.AddScript("$x");
            Task<IEnumerable<int>> resultTask = this.powerShellContext.ExecuteCommand<int>(psCommand);

            // Wait for the requested runspace handle and then dispose it
            RunspaceHandle handle = await handleTask;
            handle.Dispose();

            // At this point, the remaining command executions should execute and complete
            int result = (await resultTask).FirstOrDefault();

            // 100 + 200 = 300, then divided by 100 is 3.  We are ensuring that
            // the commands were executed in the sequence they were called.
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task CanAbortExecution()
        {
            var executeTask =
                Task.Run(
                    async () =>
                    {
                        var unusedTask = this.powerShellContext.ExecuteScriptAtPath(DebugTestFilePath);
                        await Task.Delay(50);
                        this.powerShellContext.AbortExecution();
                    });

            await this.AssertStateChange(PowerShellContextState.Running);
            await this.AssertStateChange(PowerShellContextState.Aborting);
            await this.AssertStateChange(PowerShellContextState.Ready);

            await executeTask;
        }

        #region Helper Methods

        private async Task AssertStateChange(PowerShellContextState expectedState)
        {
            SessionStateChangedEventArgs newState =
                await this.stateChangeQueue.DequeueAsync();

            Assert.Equal(expectedState, newState.NewSessionState);
        }

        private void OnSessionStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            this.stateChangeQueue.Enqueue(e);
        }

        #endregion
    }
}

