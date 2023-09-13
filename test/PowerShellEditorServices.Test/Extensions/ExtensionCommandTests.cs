// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Extensions.Services;
using Microsoft.PowerShell.EditorServices.Services.Extension;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Xunit;

namespace PowerShellEditorServices.Test.Extensions
{
    [Trait("Category", "Extensions")]
    public class ExtensionCommandTests : IDisposable
    {
        private readonly PsesInternalHost psesHost;

        private readonly ExtensionCommandService extensionCommandService;

        public ExtensionCommandTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            ExtensionService extensionService = new(
                languageServer: null,
                serviceProvider: null,
                editorOperations: null,
                executionService: psesHost);
#pragma warning disable VSTHRD002
            extensionService.InitializeAsync().Wait();
#pragma warning restore VSTHRD002
            extensionCommandService = new(extensionService);
        }

        public void Dispose()
        {
#pragma warning disable VSTHRD002
            psesHost.StopAsync().Wait();
#pragma warning restore VSTHRD002
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task CanRegisterAndInvokeCommandWithCmdletName()
        {
            string filePath = TestUtilities.NormalizePath(@"C:\Temp\Test.ps1");
            ScriptFile currentFile = new(new Uri(filePath), "This is a test file", new Version("7.0"));
            EditorContext editorContext = new(
                editorOperations: null,
                currentFile,
                new BufferPosition(line: 1, column: 1),
                BufferRange.None);

            EditorCommand commandAdded = null;
            extensionCommandService.CommandAdded += (_, command) => commandAdded = command;

            const string commandName = "test.function";
            const string commandDisplayName = "Function extension";

            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript(
                    "function Invoke-Extension { $global:extensionValue = 5 }; " +
                    $"Register-EditorCommand -Name {commandName} -DisplayName \"{commandDisplayName}\" -Function Invoke-Extension"),
                CancellationToken.None).ConfigureAwait(true);

            Assert.NotNull(commandAdded);
            Assert.Equal(commandName, commandAdded.Name);
            Assert.Equal(commandDisplayName, commandAdded.DisplayName);

            // Invoke the command
            await extensionCommandService.InvokeCommandAsync(commandName, editorContext).ConfigureAwait(true);

            // Assert the expected value
            PSCommand psCommand = new PSCommand().AddScript("$global:extensionValue");
            IEnumerable<int> results = await psesHost.ExecutePSCommandAsync<int>(psCommand, CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(5, results.FirstOrDefault());
        }

        [Fact]
        public async Task CanRegisterAndInvokeCommandWithScriptBlock()
        {
            string filePath = TestUtilities.NormalizePath(@"C:\Temp\Test.ps1");
            ScriptFile currentFile = new(new Uri(filePath), "This is a test file", new Version("7.0"));
            EditorContext editorContext = new(
                editorOperations: null,
                currentFile,
                new BufferPosition(line: 1, column: 1),
                BufferRange.None);

            EditorCommand commandAdded = null;
            extensionCommandService.CommandAdded += (_, command) => commandAdded = command;

            const string commandName = "test.scriptblock";
            const string commandDisplayName = "ScriptBlock extension";

            await psesHost.ExecutePSCommandAsync(
                new PSCommand()
                    .AddCommand("Register-EditorCommand")
                    .AddParameter("Name", commandName)
                    .AddParameter("DisplayName", commandDisplayName)
                    .AddParameter("ScriptBlock", ScriptBlock.Create("$global:extensionValue = 10")),
                CancellationToken.None).ConfigureAwait(true);

            Assert.NotNull(commandAdded);
            Assert.Equal(commandName, commandAdded.Name);
            Assert.Equal(commandDisplayName, commandAdded.DisplayName);

            // Invoke the command.
            // TODO: What task was this cancelling?
            await extensionCommandService.InvokeCommandAsync("test.scriptblock", editorContext).ConfigureAwait(true);

            // Assert the expected value
            PSCommand psCommand = new PSCommand().AddScript("$global:extensionValue");
            IEnumerable<int> results = await psesHost.ExecutePSCommandAsync<int>(psCommand, CancellationToken.None).ConfigureAwait(true);
            Assert.Equal(10, results.FirstOrDefault());
        }

        [Fact]
        public async Task CanUpdateRegisteredCommand()
        {
            EditorCommand updatedCommand = null;
            extensionCommandService.CommandUpdated += (_, command) => updatedCommand = command;

            const string commandName = "test.function";
            const string commandDisplayName = "Updated function extension";

            // Register a command and then update it
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript(
                    "function Invoke-Extension { Write-Output \"Extension output!\" }; " +
                    $"Register-EditorCommand -Name {commandName} -DisplayName \"Old function extension\" -Function Invoke-Extension; " +
                    $"Register-EditorCommand -Name {commandName} -DisplayName \"{commandDisplayName}\" -Function Invoke-Extension"),
                CancellationToken.None).ConfigureAwait(true);

            // Wait for the add and update events
            Assert.NotNull(updatedCommand);
            Assert.Equal(commandName, updatedCommand.Name);
            Assert.Equal(commandDisplayName, updatedCommand.DisplayName);
        }

        [Fact]
        public async Task CanUnregisterCommand()
        {
            string filePath = TestUtilities.NormalizePath(@"C:\Temp\Test.ps1");
            ScriptFile currentFile = new(new Uri(filePath), "This is a test file", new Version("7.0"));
            EditorContext editorContext = new(
                editorOperations: null,
                currentFile,
                new BufferPosition(line: 1, column: 1),
                BufferRange.None);

            const string commandName = "test.scriptblock";
            const string commandDisplayName = "ScriptBlock extension";

            EditorCommand removedCommand = null;
            extensionCommandService.CommandRemoved += (_, command) => removedCommand = command;

            // Add the command and wait for the add event
            await psesHost.ExecutePSCommandAsync(
                new PSCommand()
                    .AddCommand("Register-EditorCommand")
                    .AddParameter("Name", commandName)
                    .AddParameter("DisplayName", commandDisplayName)
                    .AddParameter("ScriptBlock", ScriptBlock.Create("Write-Output \"Extension output!\"")),
                CancellationToken.None).ConfigureAwait(true);

            // Remove the command and wait for the remove event
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddCommand("Unregister-EditorCommand").AddParameter("Name", commandName),
                CancellationToken.None).ConfigureAwait(true);

            Assert.NotNull(removedCommand);
            Assert.Equal(commandName, removedCommand.Name);
            Assert.Equal(commandDisplayName, removedCommand.DisplayName);

            // Ensure that the command has been unregistered
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => extensionCommandService.InvokeCommandAsync("test.scriptblock", editorContext)).ConfigureAwait(true);
        }

        [Fact]
        public async Task CannotRemovePSEditorVariable()
        {
            ActionPreferenceStopException exception = await Assert.ThrowsAsync<ActionPreferenceStopException>(
                () => psesHost.ExecutePSCommandAsync<string>(
                    new PSCommand().AddScript("Remove-Variable psEditor -ErrorAction Stop"),
                    CancellationToken.None)
            ).ConfigureAwait(true);

            Assert.Equal(
                "The running command stopped because the preference variable \"ErrorActionPreference\" or common parameter is set to Stop: Cannot remove variable psEditor because it is constant or read-only. If the variable is read-only, try the operation again specifying the Force option.",
                exception.Message);
        }
    }
}
