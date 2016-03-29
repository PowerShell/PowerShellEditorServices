//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Session;
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
        private PowerShellContext powerShellContext;
        private AsyncQueue<SessionStateChangedEventArgs> stateChangeQueue;

        private const string TestHostProfileId = "Test.PowerShellEditorServices";
        private const string DebugTestFilePath =
            @"..\..\..\PowerShellEditorServices.Test.Shared\Debugging\DebugTest.ps1";

        public PowerShellContextTests()
        {
            this.powerShellContext = new PowerShellContext(TestHostProfileId);
            this.powerShellContext.SessionStateChanged += OnSessionStateChanged;
            this.stateChangeQueue = new AsyncQueue<SessionStateChangedEventArgs>();
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

        [Fact]
        public async Task CanResolveAndLoadProfilesForHostId()
        {
            string testProfilePath =
                Path.GetFullPath(
                    @"..\..\..\PowerShellEditorServices.Test.Shared\Profile\Profile.ps1");

            string profileName =
                string.Format(
                    "{0}_{1}",
                    TestHostProfileId,
                    ProfilePaths.AllHostsProfileName);

            string currentUserPath =
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "WindowsPowerShell");
            string allUsersPath = null; // To be set later

            using (RunspaceHandle runspaceHandle = await this.powerShellContext.GetRunspaceHandle())
            {
                allUsersPath =
                    (string)runspaceHandle
                        .Runspace
                        .SessionStateProxy
                        .PSVariable
                        .Get("PsHome")
                        .Value;
            }

            string[] expectedProfilePaths =
                new string[]
                {
                    Path.Combine(allUsersPath, ProfilePaths.AllHostsProfileName),
                    Path.Combine(allUsersPath, profileName),
                    Path.Combine(currentUserPath, ProfilePaths.AllHostsProfileName),
                    Path.Combine(currentUserPath, profileName)
                };

            // Copy the test profile to the current user's host profile path
            File.Copy(testProfilePath, expectedProfilePaths[3], true);

            // Load the profiles for the test host name
            await this.powerShellContext.LoadProfilesForHost();

            // Delete the test profile before any assert failures
            // cause the function to exit
            File.Delete(expectedProfilePaths[3]);

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
                await this.powerShellContext.ExecuteCommand<string>(
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

