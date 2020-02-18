//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Extensions.Services;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Extensions
{
    // TODO:
    // These tests require being able to instantiate a language server and use the service provider.
    // Re-enable them when we have mocked out more infrastructure for testing.

    /*
    public class ExtensionCommandTests : IDisposable
    {
        private readonly PowerShellContextService _powershellContextService;

        private readonly IExtensionCommandService _extensionCommandService;

        private readonly ExtensionService _extensionService;

        public ExtensionCommandTests()
        {
            _powershellContextService = PowerShellContextFactory.Create(NullLogger.Instance);
            _extensionCommandService = EditorObject.Instance.GetExtensionServiceProvider().ExtensionCommands;
        }

        [Trait("Category", "Extensions")]
        [Fact]
        public async Task CanRegisterAndInvokeCommandWithCmdletName()
        {
            string filePath = TestUtilities.NormalizePath("C:\\Temp\\Test.ps1");
            var currentFile = new ScriptFile(new Uri(filePath), "This is a test file", new Version("7.0"));
            var editorContext = new EditorContext(
                editorOperations: null,
                currentFile,
                new BufferPosition(line: 1, column: 1),
                BufferRange.None);

            EditorCommand commandAdded = null;
            _extensionCommandService.CommandAdded += (object sender, EditorCommand command) =>
            {
                commandAdded = command;
            };

            string commandName = "test.function";
            string commandDisplayName = "Function extension";


            await _powershellContextService.ExecuteScriptStringAsync(
                TestUtilities.NormalizeNewlines($@"
function Invoke-Extension {{ $global:testValue = 5 }}
Register-EditorCommand -Name {commandName} -DisplayName ""{commandDisplayName}"" -Function Invoke-Extension"));

            Assert.NotNull(commandAdded);
            Assert.Equal(commandAdded.Name, commandName);
            Assert.Equal(commandAdded.DisplayName, commandDisplayName);

            // Invoke the command
            await _extensionCommandService.InvokeCommandAsync(commandName, editorContext);

            // Assert the expected value
            PSCommand psCommand = new PSCommand().AddScript("$global:extensionValue");
            IEnumerable<int> results = await _powershellContextService.ExecuteCommandAsync<int>(psCommand);
            Assert.Equal(5, results.FirstOrDefault());
        }

        [Trait("Category", "Extensions")]
        [Fact]
        public async Task CanRegisterAndInvokeCommandWithScriptBlock()
        {
            string filePath = TestUtilities.NormalizePath("C:\\Temp\\Test.ps1");
            var currentFile = new ScriptFile(new Uri(filePath), "This is a test file", new Version("7.0"));
            var editorContext = new EditorContext(
                editorOperations: null,
                currentFile,
                new BufferPosition(line: 1, column: 1),
                BufferRange.None);


            EditorCommand commandAdded = null;
            _extensionCommandService.CommandAdded += (object sender, EditorCommand command) =>
            {
                commandAdded = command;
            };


            string commandName = "test.scriptblock";
            string commandDisplayName = "ScriptBlock extension";

            await _powershellContextService.ExecuteCommandAsync(new PSCommand()
                .AddCommand("Register-EditorCommand")
                .AddParameter("Name", commandName)
                .AddParameter("DisplayName", commandDisplayName)
                .AddParameter("ScriptBlock", ScriptBlock.Create("$global:extensionValue = 10")));

            Assert.NotNull(commandAdded);
            Assert.Equal(commandName, commandAdded.Name);
            Assert.Equal(commandDisplayName, commandAdded.DisplayName);

            // Invoke the command
            await _extensionCommandService.InvokeCommandAsync("test.scriptblock", editorContext);

            // Assert the expected value
            PSCommand psCommand = new PSCommand().AddScript("$global:extensionValue");
            IEnumerable<int> results = await _powershellContextService.ExecuteCommandAsync<int>(psCommand);
            Assert.Equal(10, results.FirstOrDefault());
        }

        [Trait("Category", "Extensions")]
        [Fact]
        public async Task CanUpdateRegisteredCommand()
        {
            EditorCommand updatedCommand = null;
            _extensionCommandService.CommandUpdated += (object sender, EditorCommand command) =>
            {
                updatedCommand = command;
            };

            string commandName = "test.function";
            string commandDisplayName = "Updated function extension";

            // Register a command and then update it
            await _powershellContextService.ExecuteScriptStringAsync(TestUtilities.NormalizeNewlines(
                "function Invoke-Extension { Write-Output \"Extension output!\" }\n" +
                $"Register-EditorCommand -Name \"{commandName}\" -DisplayName \"Old function extension\" -Function \"Invoke-Extension\"\n" +
                $"Register-EditorCommand -Name \"{commandName}\" -DisplayName \"{commandDisplayName}\" -Function \"Invoke-Extension\""));

            // Wait for the add and update events
            Assert.NotNull(updatedCommand);
            Assert.Equal(commandName, updatedCommand.Name);
            Assert.Equal(commandDisplayName, updatedCommand.DisplayName);
        }

        [Trait("Category", "Extensions")]
        [Fact]
        public async Task CanUnregisterCommand()
        {
            string filePath = TestUtilities.NormalizePath("C:\\Temp\\Test.ps1");
            var currentFile = new ScriptFile(new Uri(filePath), "This is a test file", new Version("7.0"));
            var editorContext = new EditorContext(
                editorOperations: null,
                currentFile,
                new BufferPosition(line: 1, column: 1),
                BufferRange.None);

            string commandName = "test.scriptblock";
            string commandDisplayName = "ScriptBlock extension";

            EditorCommand removedCommand = null;
            _extensionCommandService.CommandRemoved += (object sender, EditorCommand command) =>
            {
                removedCommand = command;
            };

            // Add the command and wait for the add event
            await _powershellContextService.ExecuteCommandAsync(new PSCommand()
                .AddCommand("Register-EditorCommand")
                .AddParameter("Name", commandName)
                .AddParameter("DisplayName", commandDisplayName)
                .AddParameter("ScriptBlock", ScriptBlock.Create("Write-Output \"Extension output!\"")));

            // Remove the command and wait for the remove event
            await _powershellContextService.ExecuteCommandAsync(new PSCommand()
                .AddCommand("Unregister-EditorCommand")
                .AddParameter("Name", commandName));

            Assert.NotNull(removedCommand);
            Assert.Equal(commandName, removedCommand.Name);
            Assert.Equal(commandDisplayName, removedCommand.DisplayName);

            // Ensure that the command has been unregistered
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _extensionCommandService.InvokeCommandAsync("test.scriptblock", editorContext));
        }

        public void Dispose()
        {
            _powershellContextService.Dispose();
        }
    }
    */
}

