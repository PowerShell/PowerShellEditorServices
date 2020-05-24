//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class PowerShellContextTests : IDisposable
    {
        private PowerShellContextService powerShellContext;
        private AsyncQueue<SessionStateChangedEventArgs> stateChangeQueue;

        private static readonly string s_debugTestFilePath =
            TestUtilities.NormalizePath("../../../../PowerShellEditorServices.Test.Shared/Debugging/DebugTest.ps1");

        public PowerShellContextTests()
        {
            this.powerShellContext = PowerShellContextFactory.Create(NullLogger.Instance);
            this.powerShellContext.SessionStateChanged += OnSessionStateChanged;
            this.stateChangeQueue = new AsyncQueue<SessionStateChangedEventArgs>();
        }

        public void Dispose()
        {
            this.powerShellContext.Close();
            this.powerShellContext = null;
        }

        [Trait("Category", "PowerShellContext")]
        [Fact]
        public async Task CanExecutePSCommand()
        {
            PSCommand psCommand = new PSCommand();
            psCommand.AddScript("$a = \"foo\"; $a");

            var executeTask =
                this.powerShellContext.ExecuteCommandAsync<string>(psCommand);

            await this.AssertStateChange(PowerShellContextState.Running);
            await this.AssertStateChange(PowerShellContextState.Ready);

            var result = await executeTask;
            Assert.Equal("foo", result.First());
        }

        [Trait("Category", "PowerShellContext")]
        [Fact]
        public async Task CanQueueParallelRunspaceRequests()
        {
            // Concurrently initiate 4 requests in the session
            Task taskOne = this.powerShellContext.ExecuteScriptStringAsync("$x = 100");
            Task<RunspaceHandle> handleTask = this.powerShellContext.GetRunspaceHandleAsync();
            Task taskTwo = this.powerShellContext.ExecuteScriptStringAsync("$x += 200");
            Task taskThree = this.powerShellContext.ExecuteScriptStringAsync("$x = $x / 100");

            PSCommand psCommand = new PSCommand();
            psCommand.AddScript("$x");
            Task<IEnumerable<int>> resultTask = this.powerShellContext.ExecuteCommandAsync<int>(psCommand);

            // Wait for the requested runspace handle and then dispose it
            RunspaceHandle handle = await handleTask;
            handle.Dispose();

            // Wait for all of the executes to complete
            await Task.WhenAll(taskOne, taskTwo, taskThree, resultTask);

            // At this point, the remaining command executions should execute and complete
            int result = resultTask.Result.FirstOrDefault();

            // 100 + 200 = 300, then divided by 100 is 3.  We are ensuring that
            // the commands were executed in the sequence they were called.
            Assert.Equal(3, result);
        }

        [Trait("Category", "PowerShellContext")]
        [Fact]
        public async Task CanAbortExecution()
        {
            var executeTask =
                Task.Run(
                    async () =>
                    {
                        var unusedTask = this.powerShellContext.ExecuteScriptWithArgsAsync(s_debugTestFilePath);
                        await Task.Delay(50);
                        this.powerShellContext.AbortExecution();
                    });

            await this.AssertStateChange(PowerShellContextState.Running);
            await this.AssertStateChange(PowerShellContextState.Aborting);
            await this.AssertStateChange(PowerShellContextState.Ready);

            await executeTask;
        }

        [Trait("Category", "PowerShellContext")]
        [Fact]
        public async Task CanResolveAndLoadProfilesForHostId()
        {
            string[] expectedProfilePaths =
                new string[]
                {
                    PowerShellContextFactory.TestProfilePaths.AllUsersAllHosts,
                    PowerShellContextFactory.TestProfilePaths.AllUsersCurrentHost,
                    PowerShellContextFactory.TestProfilePaths.CurrentUserAllHosts,
                    PowerShellContextFactory.TestProfilePaths.CurrentUserCurrentHost
                };

            // Load the profiles for the test host name
            await this.powerShellContext.LoadHostProfilesAsync();

            // Ensure that all the paths are set in the correct variables
            // and that the current user's host profile got loaded
            PSCommand psCommand = new PSCommand();
            psCommand.AddScript(
                "\"$($profile.AllUsersAllHosts) " +
                "$($profile.AllUsersCurrentHost) " +
                "$($profile.CurrentUserAllHosts) " +
                "$($profile.CurrentUserCurrentHost) " +
                "$(Assert-ProfileLoaded)\"");

            var result =
                await this.powerShellContext.ExecuteCommandAsync<string>(
                    psCommand);

            string expectedString =
                string.Format(
                    "{0} True",
                    string.Join(
                        " ",
                        expectedProfilePaths));

            Assert.Equal(expectedString, result.FirstOrDefault(), true);
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
            this.stateChangeQueue.EnqueueAsync(e).Wait();
        }

        #endregion
    }
}

