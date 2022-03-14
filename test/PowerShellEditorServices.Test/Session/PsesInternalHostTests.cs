// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Console
{
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;

    [Trait("Category", "PsesInternalHost")]
    public class PsesInternalHostTests : IDisposable
    {
        private readonly PsesInternalHost psesHost;

        public PsesInternalHostTests() => psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);

        public void Dispose()
        {
            psesHost.StopAsync().Wait();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task CanExecutePSCommand()
        {
            Assert.True(psesHost.IsRunning);
            PSCommand command = new PSCommand().AddScript("$a = \"foo\"; $a");
            Task<IReadOnlyList<string>> task = psesHost.ExecutePSCommandAsync<string>(command, CancellationToken.None);
            IReadOnlyList<string> result = await task.ConfigureAwait(true);
            Assert.Equal("foo", result[0]);
        }

        [Fact] // https://github.com/PowerShell/vscode-powershell/issues/3677
        public async Task CanHandleThrow()
        {
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("throw"),
                CancellationToken.None,
                new PowerShellExecutionOptions { ThrowOnError = false }).ConfigureAwait(true);
        }

        [Fact]
        public async Task CanQueueParallelPSCommands()
        {
            // Concurrently initiate 4 requests in the session.
            Task taskOne = psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("$x = 100"),
                CancellationToken.None);

            Task taskTwo = psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("$x += 200"),
                CancellationToken.None);

            Task taskThree = psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("$x = $x / 100"),
                CancellationToken.None);

            Task<IReadOnlyList<int>> resultTask = psesHost.ExecutePSCommandAsync<int>(
                new PSCommand().AddScript("$x"),
                CancellationToken.None);

            // Wait for all of the executes to complete.
            await Task.WhenAll(taskOne, taskTwo, taskThree, resultTask).ConfigureAwait(true);

            // Sanity checks
            Assert.Equal(RunspaceState.Opened, psesHost.Runspace.RunspaceStateInfo.State);

            // 100 + 200 = 300, then divided by 100 is 3.  We are ensuring that
            // the commands were executed in the sequence they were called.
            Assert.Equal(3, (await resultTask.ConfigureAwait(true))[0]);
        }

        [Fact]
        public async Task CanCancelExecutionWithToken()
        {
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
            {
                return psesHost.ExecutePSCommandAsync(
                    new PSCommand().AddScript("Start-Sleep 10"),
                    new CancellationTokenSource(1000).Token);
            }).ConfigureAwait(true);
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Explicitly checking task cancellation status.")]
        public async Task CanCancelExecutionWithMethod()
        {
            Task executeTask = psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("Start-Sleep 10"),
                CancellationToken.None);

            // Wait until our task has started.
            Thread.Sleep(2000);
            psesHost.CancelCurrentTask();
            await Assert.ThrowsAsync<TaskCanceledException>(() => executeTask).ConfigureAwait(true);
            Assert.True(executeTask.IsCanceled);
        }

        [Fact]
        public async Task CanResolveAndLoadProfilesForHostId()
        {
            string[] expectedProfilePaths =
                new string[]
                {
                    PsesHostFactory.TestProfilePaths.AllUsersAllHosts,
                    PsesHostFactory.TestProfilePaths.AllUsersCurrentHost,
                    PsesHostFactory.TestProfilePaths.CurrentUserAllHosts,
                    PsesHostFactory.TestProfilePaths.CurrentUserCurrentHost
                };

            // Load the profiles for the test host name
            await psesHost.LoadHostProfilesAsync(CancellationToken.None).ConfigureAwait(true);

            // Ensure that all the paths are set in the correct variables
            // and that the current user's host profile got loaded
            PSCommand psCommand = new PSCommand().AddScript(
                "\"$($profile.AllUsersAllHosts) " +
                "$($profile.AllUsersCurrentHost) " +
                "$($profile.CurrentUserAllHosts) " +
                "$($profile.CurrentUserCurrentHost) " +
                "$(Assert-ProfileLoaded)\"");

            IReadOnlyList<string> result = await psesHost.ExecutePSCommandAsync<string>(psCommand, CancellationToken.None).ConfigureAwait(true);

            string expectedString =
                string.Format(
                    "{0} True",
                    string.Join(
                        " ",
                        expectedProfilePaths));

            Assert.Equal(expectedString, result[0], ignoreCase: true);
        }

        [Fact]
        public async Task CanLoadPSReadLine()
        {
            // NOTE: This is slightly more complicated than one would expect because we explicitly
            // need it to run on the pipeline thread otherwise Windows complains about the the
            // thread's appartment state not matching.
            Assert.True(await psesHost.ExecuteDelegateAsync(
                nameof(psesHost.TryLoadPSReadLine),
                executionOptions: null,
                (pwsh, _) => psesHost.TryLoadPSReadLine(
                    pwsh,
                    (EngineIntrinsics)pwsh.Runspace.SessionStateProxy.GetVariable("ExecutionContext"),
                    out IReadLine readLine),
                CancellationToken.None).ConfigureAwait(true));
        }
    }
}
