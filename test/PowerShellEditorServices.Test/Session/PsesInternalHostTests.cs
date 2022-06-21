// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Hosting;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Console;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
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
            // Load the profiles for the test host name
            await psesHost.LoadHostProfilesAsync(CancellationToken.None).ConfigureAwait(true);

            // Ensure that the $PROFILE variable is a string with the value of CurrentUserCurrentHost.
            IReadOnlyList<string> profileVariable = await psesHost.ExecutePSCommandAsync<string>(
                new PSCommand().AddScript("$PROFILE"),
                CancellationToken.None).ConfigureAwait(true);

            Assert.Collection(profileVariable,
                (p) => Assert.Equal(PsesHostFactory.TestProfilePaths.CurrentUserCurrentHost, p));

            // Ensure that all the profile paths are set in the correct note properties.
            IReadOnlyList<string> profileProperties = await psesHost.ExecutePSCommandAsync<string>(
                new PSCommand().AddScript("$PROFILE | Get-Member -Type NoteProperty"),
                CancellationToken.None).ConfigureAwait(true);

            Assert.Collection(profileProperties,
                (p) => Assert.Equal($"string AllUsersAllHosts={PsesHostFactory.TestProfilePaths.AllUsersAllHosts}", p, ignoreCase: true),
                (p) => Assert.Equal($"string AllUsersCurrentHost={PsesHostFactory.TestProfilePaths.AllUsersCurrentHost}", p, ignoreCase: true),
                (p) => Assert.Equal($"string CurrentUserAllHosts={PsesHostFactory.TestProfilePaths.CurrentUserAllHosts}", p, ignoreCase: true),
                (p) => Assert.Equal($"string CurrentUserCurrentHost={PsesHostFactory.TestProfilePaths.CurrentUserCurrentHost}", p, ignoreCase: true));

            // Ensure that the profile was loaded. The profile also checks that $PROFILE was defined.
            IReadOnlyList<bool> profileLoaded = await psesHost.ExecutePSCommandAsync<bool>(
                new PSCommand().AddScript("Assert-ProfileLoaded"),
                CancellationToken.None).ConfigureAwait(true);

            Assert.Collection(profileLoaded, (p) => Assert.True(p));
        }

        [Fact]
        public async Task CanHandleNoProfiles()
        {
            // Call LoadProfiles with profile paths that won't exist, and assert that it does not
            // throw PSInvalidOperationException (which it previously did when it tried to invoke an
            // empty command).
            ProfilePathInfo emptyProfilePaths = new("", "", "", "");
            await psesHost.ExecuteDelegateAsync(
                "LoadProfiles",
                executionOptions: null,
                (pwsh, _) => {
                    pwsh.LoadProfiles(emptyProfilePaths);
                    Assert.Empty(pwsh.Commands.Commands);
                },
                CancellationToken.None).ConfigureAwait(true);
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

        // This test asserts that we do not mess up the console encoding, which leads to native
        // commands receiving piped input failing.
        [Fact]
        public async Task ExecutesNativeCommandsCorrectly()
        {
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("\"protocol=https`nhost=myhost.com`nusername=john`npassword=doe`n`n\" | git.exe credential approve; if ($LastExitCode) { throw }"),
                CancellationToken.None).ConfigureAwait(true);
        }
    }
}
