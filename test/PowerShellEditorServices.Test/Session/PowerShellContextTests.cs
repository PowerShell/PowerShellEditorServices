// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Utility;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    public class PowerShellContextTests : IDisposable
    {
        // Borrowed from `VersionUtils` which can't be used here due to an initialization problem.
        private static bool IsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

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

            await this.AssertStateChange(PowerShellContextState.Running).ConfigureAwait(false);
            await this.AssertStateChange(PowerShellContextState.Ready).ConfigureAwait(false);

            var result = await executeTask.ConfigureAwait(false);
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
            RunspaceHandle handle = await handleTask.ConfigureAwait(false);
            handle.Dispose();

            // Wait for all of the executes to complete
            await Task.WhenAll(taskOne, taskTwo, taskThree, resultTask).ConfigureAwait(false);

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
                        await Task.Delay(50).ConfigureAwait(false);
                        this.powerShellContext.AbortExecution();
                    });

            await this.AssertStateChange(PowerShellContextState.Running).ConfigureAwait(false);
            await this.AssertStateChange(PowerShellContextState.Aborting).ConfigureAwait(false);
            await this.AssertStateChange(PowerShellContextState.Ready).ConfigureAwait(false);

            await executeTask.ConfigureAwait(false);
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
            await this.powerShellContext.LoadHostProfilesAsync().ConfigureAwait(false);

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
                    psCommand).ConfigureAwait(false);

            string expectedString =
                string.Format(
                    "{0} True",
                    string.Join(
                        " ",
                        expectedProfilePaths));

            Assert.Equal(expectedString, result.FirstOrDefault(), true);
        }

        [Trait("Category", "PSReadLine")]
        [Fact]
        public void CanGetPSReadLineProxy()
        {
            // This will force the loading of the PSReadLine assembly
            var psContext = PowerShellContextFactory.Create(NullLogger.Instance, isPSReadLineEnabled: true);
            Assert.True(PSReadLinePromptContext.TryGetPSReadLineProxy(
                NullLogger.Instance,
                out PSReadLineProxy proxy));
        }

        #region Helper Methods

        private async Task AssertStateChange(PowerShellContextState expectedState)
        {
            SessionStateChangedEventArgs newState =
                await this.stateChangeQueue.DequeueAsync().ConfigureAwait(false);

            Assert.Equal(expectedState, newState.NewSessionState);
        }

        private void OnSessionStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            this.stateChangeQueue.EnqueueAsync(e).Wait();
        }

        #endregion
    }
}
