// TODO: Bring this back with API refactor.

// //
// // Copyright (c) Microsoft. All rights reserved.
// // Licensed under the MIT license. See LICENSE file in the project root for full license information.
// //

// using Microsoft.Extensions.Logging.Abstractions;
// using Microsoft.PowerShell.EditorServices.Services;
// using Microsoft.PowerShell.EditorServices.Services.PowerShellContext;
// using Microsoft.PowerShell.EditorServices.Services.TextDocument;
// using Microsoft.PowerShell.EditorServices.Test.Shared;
// using Microsoft.PowerShell.EditorServices.Utility;
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Management.Automation;
// using System.Threading;
// using System.Threading.Tasks;
// using Xunit;

// namespace Microsoft.PowerShell.EditorServices.Test.Extensions
// {
//     public class ExtensionServiceTests : IAsyncLifetime
//     {
//         private ScriptFile currentFile;
//         private EditorContext commandContext;
//         private ExtensionService extensionService;
//         private PowerShellContextService powerShellContext;
//         private TestEditorOperations editorOperations;

//         private AsyncQueue<Tuple<EventType, EditorCommand>> extensionEventQueue =
//             new AsyncQueue<Tuple<EventType, EditorCommand>>();

//         private enum EventType
//         {
//             Add,
//             Update,
//             Remove
//         }

//         public async Task InitializeAsync()
//         {
//             var logger = NullLogger.Instance;
//             this.powerShellContext = PowerShellContextFactory.Create(logger);
//             await this.powerShellContext.ImportCommandsModuleAsync(
//                 TestUtilities.NormalizePath("../../../../../module/PowerShellEditorServices/Commands"));

//             this.extensionService = new ExtensionService(this.powerShellContext, null);
//             this.editorOperations = new TestEditorOperations();

//             this.extensionService.CommandAdded += ExtensionService_ExtensionAdded;
//             this.extensionService.CommandUpdated += ExtensionService_ExtensionUpdated;
//             this.extensionService.CommandRemoved += ExtensionService_ExtensionRemoved;

//             await this.extensionService.InitializeAsync(
//                 serviceProvider: null,
//                 this.editorOperations);

//             var filePath = TestUtilities.NormalizePath("c:/Test/Test.ps1");
//             this.currentFile = new ScriptFile(new Uri(filePath), "This is a test file", new Version("5.0"));
//             this.commandContext =
//                 new EditorContext(
//                     this.editorOperations,
//                     currentFile,
//                     new BufferPosition(1, 1),
//                     BufferRange.None);
//         }

//         public Task DisposeAsync()
//         {
//             this.powerShellContext.Dispose();
//             return Task.FromResult(true);
//         }

//         [Trait("Category", "Extensions")]
//         [Fact]
//         public async Task CanRegisterAndInvokeCommandWithCmdletName()
//         {
//             await extensionService.PowerShellContext.ExecuteScriptStringAsync(
//                 TestUtilities.NormalizeNewlines("function Invoke-Extension { $global:extensionValue = 5 }\n") +
//                 "Register-EditorCommand -Name \"test.function\" -DisplayName \"Function extension\" -Function \"Invoke-Extension\"");

//             // Wait for the add event
//             EditorCommand command = await this.AssertExtensionEvent(EventType.Add, "test.function");

//             // Invoke the command
//             await extensionService.InvokeCommandAsync("test.function", this.commandContext);

//             // Assert the expected value
//             PSCommand psCommand = new PSCommand();
//             psCommand.AddScript("$global:extensionValue");
//             var results = await powerShellContext.ExecuteCommandAsync<int>(psCommand);
//             Assert.Equal(5, results.FirstOrDefault());
//         }

//         [Trait("Category", "Extensions")]
//         [Fact]
//         public async Task CanRegisterAndInvokeCommandWithScriptBlock()
//         {
//             await extensionService.PowerShellContext.ExecuteScriptStringAsync(
//                 "Register-EditorCommand -Name \"test.scriptblock\" -DisplayName \"ScriptBlock extension\" -ScriptBlock { $global:extensionValue = 10 }");

//             // Wait for the add event
//             EditorCommand command = await this.AssertExtensionEvent(EventType.Add, "test.scriptblock");

//             // Invoke the command
//             await extensionService.InvokeCommandAsync("test.scriptblock", this.commandContext);

//             // Assert the expected value
//             PSCommand psCommand = new PSCommand();
//             psCommand.AddScript("$global:extensionValue");
//             var results = await powerShellContext.ExecuteCommandAsync<int>(psCommand);
//             Assert.Equal(10, results.FirstOrDefault());
//         }

//         [Trait("Category", "Extensions")]
//         [Fact]
//         public async Task CanUpdateRegisteredCommand()
//         {
//             // Register a command and then update it
//             await extensionService.PowerShellContext.ExecuteScriptStringAsync(TestUtilities.NormalizeNewlines(
//                 "function Invoke-Extension { Write-Output \"Extension output!\" }\n" +
//                 "Register-EditorCommand -Name \"test.function\" -DisplayName \"Function extension\" -Function \"Invoke-Extension\"\n" +
//                 "Register-EditorCommand -Name \"test.function\" -DisplayName \"Updated Function extension\" -Function \"Invoke-Extension\""));

//             // Wait for the add and update events
//             await this.AssertExtensionEvent(EventType.Add, "test.function");
//             EditorCommand updatedCommand = await this.AssertExtensionEvent(EventType.Update, "test.function");

//             Assert.Equal("Updated Function extension", updatedCommand.DisplayName);
//         }

//         [Trait("Category", "Extensions")]
//         [Fact]
//         public async Task CanUnregisterCommand()
//         {
//             // Add the command and wait for the add event
//             await extensionService.PowerShellContext.ExecuteScriptStringAsync(
//                 "Register-EditorCommand -Name \"test.scriptblock\" -DisplayName \"ScriptBlock extension\" -ScriptBlock { Write-Output \"Extension output!\" }");
//             await this.AssertExtensionEvent(EventType.Add, "test.scriptblock");

//             // Remove the command and wait for the remove event
//             await extensionService.PowerShellContext.ExecuteScriptStringAsync(
//                 "Unregister-EditorCommand -Name \"test.scriptblock\"");
//             await this.AssertExtensionEvent(EventType.Remove, "test.scriptblock");

//             // Ensure that the command has been unregistered
//             await Assert.ThrowsAsync<KeyNotFoundException>(() =>
//                 extensionService.InvokeCommandAsync("test.scriptblock", this.commandContext));
//         }

//         private async Task<EditorCommand> AssertExtensionEvent(EventType expectedEventType, string expectedExtensionName)
//         {
//             var eventExtensionTuple =
//                 await this.extensionEventQueue.DequeueAsync(
//                     new CancellationTokenSource(5000).Token);

//             Assert.Equal(expectedEventType, eventExtensionTuple.Item1);
//             Assert.Equal(expectedExtensionName, eventExtensionTuple.Item2.Name);

//             return eventExtensionTuple.Item2;
//         }

//         private async void ExtensionService_ExtensionAdded(object sender, EditorCommand e)
//         {
//             await this.extensionEventQueue.EnqueueAsync(
//                 new Tuple<EventType, EditorCommand>(EventType.Add, e));
//         }

//         private async void ExtensionService_ExtensionUpdated(object sender, EditorCommand e)
//         {
//             await this.extensionEventQueue.EnqueueAsync(
//                 new Tuple<EventType, EditorCommand>(EventType.Update, e));
//         }

//         private async void ExtensionService_ExtensionRemoved(object sender, EditorCommand e)
//         {
//             await this.extensionEventQueue.EnqueueAsync(
//                 new Tuple<EventType, EditorCommand>(EventType.Remove, e));
//         }
//     }

//     public class TestEditorOperations : IEditorOperations
//     {

//         public string GetWorkspacePath()
//         {
//             throw new NotImplementedException();
//         }

//         public string GetWorkspaceRelativePath(string filePath)
//         {
//             throw new NotImplementedException();
//         }

//         public Task NewFileAsync()
//         {
//             throw new NotImplementedException();
//         }

//         public Task OpenFileAsync(string filePath)
//         {
//             throw new NotImplementedException();
//         }

//         public Task OpenFileAsync(string filePath, bool preview)
//         {
//             throw new NotImplementedException();
//         }

//         public Task CloseFileAsync(string filePath)
//         {
//             throw new NotImplementedException();
//         }

//         public Task SaveFileAsync(string filePath)
//         {
//             return SaveFileAsync(filePath, null);
//         }

//         public Task SaveFileAsync(string filePath, string newSavePath)
//         {
//             throw new NotImplementedException();
//         }

//         public Task InsertTextAsync(string filePath, string text, BufferRange insertRange)
//         {
//             throw new NotImplementedException();
//         }

//         public Task SetSelectionAsync(BufferRange selectionRange)
//         {
//             throw new NotImplementedException();
//         }

//         public Task<EditorContext> GetEditorContextAsync()
//         {
//             throw new NotImplementedException();
//         }

//         public Task ShowInformationMessageAsync(string message)
//         {
//             throw new NotImplementedException();
//         }

//         public Task ShowErrorMessageAsync(string message)
//         {
//             throw new NotImplementedException();
//         }

//         public Task ShowWarningMessageAsync(string message)
//         {
//             throw new NotImplementedException();
//         }

//         public Task SetStatusBarMessageAsync(string message, int? timeout)
//         {
//             throw new NotImplementedException();
//         }

//         public void ClearTerminal()
//         {
//             throw new NotImplementedException();
//         }
//     }
// }

