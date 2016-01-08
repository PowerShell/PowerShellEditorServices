//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.Client;
using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Protocol.Messages;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class LanguageServerTests : IAsyncLifetime
    {
        private LanguageServiceClient languageServiceClient;

        private ConcurrentDictionary<string, AsyncProducerConsumerQueue<object>> eventQueuePerType =
            new ConcurrentDictionary<string, AsyncProducerConsumerQueue<object>>();

        public Task InitializeAsync()
        {
            string testLogPath =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "logs",
                    this.GetType().Name,
                    Guid.NewGuid().ToString().Substring(0, 8) + ".log");

            System.Console.WriteLine("        Output log at path: {0}", testLogPath);

            this.languageServiceClient =
                new LanguageServiceClient(
                    new StdioClientChannel(
                        "Microsoft.PowerShell.EditorServices.Host.exe",
                        "/logPath:\"" + testLogPath + "\""));

            return this.languageServiceClient.Start();
        }

        public Task DisposeAsync()
        {
            return this.languageServiceClient.Stop();
        }

        [Fact]
        public async Task ServiceReturnsSyntaxErrors()
        {
            // Send the 'didOpen' event
            await this.SendOpenFileEvent("TestFiles\\SimpleSyntaxError.ps1", false);

            // Wait for the diagnostic event
            PublishDiagnosticsNotification diagnostics = 
                await this.WaitForEvent(
                    PublishDiagnosticsNotification.Type);

            // Was there a syntax error?
            Assert.NotEqual(0, diagnostics.Diagnostics.Length);
            Assert.False(
                string.IsNullOrEmpty(diagnostics.Diagnostics[0].Message));
        }

        [Fact]
        public async Task ServiceCompletesFunctionName()
        {
            await this.SendOpenFileEvent("TestFiles\\CompleteFunctionName.ps1");

            CompletionItem[] completions =
                await this.SendRequest(
                    CompletionRequest.Type,
                    new TextDocumentPosition
                    {
                        Uri = "TestFiles\\CompleteFunctionName.ps1",
                        Position = new Position
                        {
                            Line = 4,
                            Character = 3,
                        }
                    });

            Assert.NotNull(completions);
            Assert.NotEqual(completions.Length, 0);

            // TODO: Add more asserts
        }

        [Fact]
        public async Task CompletesDetailOnVariableSuggestion()
        {
            await this.SendOpenFileEvent("TestFiles\\CompleteFunctionName.ps1");

            CompletionItem[] completions =
                await this.SendRequest(
                    CompletionRequest.Type,
                    new TextDocumentPosition
                    {
                        Uri = "TestFiles\\CompleteFunctionName.ps1",
                        Position = new Position
                        {
                            Line = 3,
                            Character = 5
                        }
                    });

            CompletionItem consoleFileNameItem =
                completions
                    .FirstOrDefault(
                        c => c.Label == "$ConsoleFileName");

            Assert.NotNull(consoleFileNameItem);
            Assert.Equal("string", consoleFileNameItem.Detail);
        }

        [Fact(Skip = "Skipped until variable documentation gathering is added back.")]
        public async Task CompletesDetailOnVariableDocSuggestion()
        {
            //await this.SendOpenFileEvent("TestFiles\\CompleteFunctionName.ps1");

            //await this.SendRequest(
            //    CompletionRequest.Type,
            //    new TextDocumentPosition
            //    {
            //        Uri = "TestFiles\\CompleteFunctionName.ps1",
            //        Position = new Position
            //        {
            //            Line = 7,
            //            Character = 5
            //        }
            //    });

            //CompletionsResponse completion = this.WaitForMessage<CompletionsResponse>();
            //List<string> entryName = new List<string>();
            //entryName.Add("$HKCU:");
            //await this.MessageWriter.WriteMessage(
            //    new CompletionDetailsRequest
            //    {
            //        Arguments = new CompletionDetailsRequestArgs
            //        {
            //            File = "TestFiles\\CompleteFunctionName.ps1",
            //            Line = 7,
            //            Offset = 5,
            //            EntryNames = entryName.ToArray()
            //        }
            //    });
            //CompletionDetailsResponse completionDetail = this.WaitForMessage<CompletionDetailsResponse>();
            //Assert.NotNull(completionDetail.Body[0]);
            //Assert.Equal("The software settings for the current user", completionDetail.Body[0].DocString);
        }

        [Fact]
        public async Task CompletesDetailOnCommandSuggestion()
        {
            await this.SendOpenFileEvent("TestFiles\\CompleteFunctionName.ps1");

            CompletionItem[] completions =
                await this.SendRequest(
                    CompletionRequest.Type,
                    new TextDocumentPosition
                    {
                        Uri = "TestFiles\\CompleteFunctionName.ps1",
                        Position = new Position
                        {
                            Line = 5,
                            Character = 8
                        }
                    });

            CompletionItem completionItem =
                completions
                    .FirstOrDefault(
                        c => c.Label == "Get-Process");

            Assert.NotNull(completionItem);

            CompletionItem updatedCompletionItem =
                await this.SendRequest(
                    CompletionResolveRequest.Type,
                    completionItem);

            // Can't depend on a particular documentation string if the test machine
            // hasn't run Update-Help, so just verify that a non-empty string was
            // returned.
            Assert.NotNull(updatedCompletionItem);
            Assert.True(updatedCompletionItem.Documentation.Length > 0);
        }

        [Fact]
        public async Task FindsReferencesOfVariable()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
            await this.SendRequest(
                ReferencesRequest.Type,
                new ReferencesParams
                {
                    Uri = "TestFiles\\FindReferences.ps1",
                    Position = new Position
                    {
                        Line = 7,
                        Character = 4,
                    }
                });

            Assert.NotNull(locations);
            Assert.Equal(locations.Length, 3);

            Assert.Equal(5, locations[0].Range.Start.Line);
            Assert.Equal(0, locations[0].Range.Start.Character);
            Assert.Equal(7, locations[1].Range.Start.Line);
            Assert.Equal(0, locations[1].Range.Start.Character);
            Assert.Equal(8, locations[2].Range.Start.Line);
            Assert.Equal(12, locations[2].Range.Start.Character);
        }

        [Fact]
        public async Task FindsNoReferencesOfEmptyLine()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
                await this.SendRequest(
                    ReferencesRequest.Type,
                    new ReferencesParams
                    {
                        Uri = "TestFiles\\FindReferences.ps1",
                        Position = new Position
                        {
                            Line = 9,
                            Character = 0,
                        }
                    });

            Assert.NotNull(locations);
            Assert.Equal(0, locations.Length);
        }

        [Fact]
        public async Task FindsReferencesOnFunctionDefinition()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
                await this.SendRequest(
                    ReferencesRequest.Type,
                    new ReferencesParams
                    {
                        Uri = "TestFiles\\FindReferences.ps1",
                        Position = new Position
                        {
                            Line = 0,
                            Character = 17,
                        }
                    });

            Assert.NotNull(locations);
            Assert.Equal(3, locations.Length);

            Assert.Equal(0, locations[0].Range.Start.Line);
            Assert.Equal(9, locations[0].Range.Start.Character);
            Assert.Equal(2, locations[1].Range.Start.Line);
            Assert.Equal(4, locations[1].Range.Start.Character);
            Assert.Equal(8, locations[2].Range.Start.Line);
            Assert.Equal(0, locations[2].Range.Start.Character);
        }

        [Fact]
        public async Task FindsReferencesOnCommand()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
                await this.SendRequest(
                    ReferencesRequest.Type,
                    new ReferencesParams
                    {
                        Uri = "TestFiles\\FindReferences.ps1",
                        Position = new Position
                        {
                            Line = 0,
                            Character = 17,
                        }
                    });

            Assert.NotNull(locations);
            Assert.Equal(3, locations.Length);

            Assert.Equal(0, locations[0].Range.Start.Line);
            Assert.Equal(9, locations[0].Range.Start.Character);
            Assert.Equal(2, locations[1].Range.Start.Line);
            Assert.Equal(4, locations[1].Range.Start.Character);
            Assert.Equal(8, locations[2].Range.Start.Line);
            Assert.Equal(0, locations[2].Range.Start.Character);
        }

        [Fact]
        public async Task FindsDefinitionOfCommand()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
                await this.SendRequest(
                    DefinitionRequest.Type,
                    new TextDocumentPosition
                    {
                        Uri = "TestFiles\\FindReferences.ps1",
                        Position = new Position
                        {
                            Line = 2,
                            Character = 11,
                        }
                    });

            Assert.NotNull(locations);
            Assert.Equal(1, locations.Length);
            Assert.Equal(0, locations[0].Range.Start.Line);
            Assert.Equal(9, locations[0].Range.Start.Character);
        }

        [Fact]
        public async Task FindsNoDefinitionOfBuiltinCommand()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
                await this.SendRequest(
                    DefinitionRequest.Type,
                    new TextDocumentPosition
                    {
                        Uri = "TestFiles\\FindReferences.ps1",
                        Position = new Position
                        {
                            Line = 10,
                            Character = 9,
                        }
                    });

            Assert.NotNull(locations);
            Assert.Equal(0, locations.Length);
        }

        [Fact]
        public async Task FindsDefintionOfVariable()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
                await this.SendRequest(
                    DefinitionRequest.Type,
                    new TextDocumentPosition
                    {
                        Uri = "TestFiles\\FindReferences.ps1",
                        Position = new Position
                        {
                            Line = 8,
                            Character = 13,
                        }
                    });

            Assert.NotNull(locations);
            Assert.Equal(1, locations.Length);
            Assert.Equal(5, locations[0].Range.Start.Line);
            Assert.Equal(0, locations[0].Range.Start.Character);
            Assert.Equal(5, locations[0].Range.End.Line);
            Assert.Equal(7, locations[0].Range.End.Character);
        }

        [Fact]
        public async Task FindsOccurencesOnFunctionDefinition()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            DocumentHighlight[] highlights =
                await this.SendRequest(
                    DocumentHighlightRequest.Type,
                    new TextDocumentPosition
                    {
                        Uri = "TestFiles\\FindReferences.ps1",
                        Position = new Position
                        {
                            Line = 0,
                            Character = 17,
                        }
                    });

            Assert.NotNull(highlights);
            Assert.Equal(3, highlights.Length);
            Assert.Equal(2, highlights[1].Range.Start.Line);
        }

        [Fact]
        public async Task GetsParameterHintsOnCommand()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            SignatureHelp signatureHelp =
                await this.SendRequest(
                    SignatureHelpRequest.Type,
                    new TextDocumentPosition
                    {
                        Uri = "TestFiles\\FindReferences.ps1",
                        Position = new Position
                        {
                            Line = 12,
                            Character = 14
                        }
                    });

            Assert.NotNull(signatureHelp);
            Assert.Equal(1, signatureHelp.Signatures.Length);
            Assert.Equal(2, signatureHelp.Signatures[0].Parameters.Length);
            Assert.Equal(
                "Write-Output [-InputObject] <psobject[]> [-NoEnumerate] [<CommonParameters>]",
                signatureHelp.Signatures[0].Label);
        }

        [Fact]
        public async Task ServiceExecutesReplCommandAndReceivesOutput()
        {
            this.QueueEventsForType(OutputEvent.Type);

            await 
                this.SendRequest(
                    EvaluateRequest.Type,
                    new EvaluateRequestArguments
                    {
                        Expression = "1 + 2"
                    });

            OutputEventBody outputEvent = await this.WaitForEvent(OutputEvent.Type);
            Assert.Equal("1 + 2\r\n\r\n", outputEvent.Output);
            Assert.Equal("stdout", outputEvent.Category);

            outputEvent = await this.WaitForEvent(OutputEvent.Type);
            Assert.Equal("3\r\n", outputEvent.Output);
            Assert.Equal("stdout", outputEvent.Category);
        }

        [Fact]
        public async Task ServiceExpandsAliases()
        {
            string expandedText =
                await this.SendRequest(
                    ExpandAliasRequest.Type,
                    "gci\r\npwd");

            Assert.Equal("Get-ChildItem\r\nGet-Location", expandedText);
        }

        [Fact]
        public async Task ServiceExecutesReplCommandAndReceivesChoicePrompt()
        {
            string choiceScript =
                @"
                $caption = ""Test Choice"";
                $message = ""Make a selection"";
                $choiceA = New-Object System.Management.Automation.Host.ChoiceDescription ""&Apple"",""Help for Apple"";
                $choiceB = New-Object System.Management.Automation.Host.ChoiceDescription ""Banana"",""Help for Banana"";
                $choices = [System.Management.Automation.Host.ChoiceDescription[]]($choiceA,$choiceB);
                $host.ui.PromptForChoice($caption, $message, $choices, 1)";

            Task<ShowChoicePromptNotification> choicePromptTask =
                this.WaitForEvent(ShowChoicePromptNotification.Type);

            // Execute the script but don't await the task yet because
            // the choice prompt will block execution from completing
            Task<EvaluateResponseBody> evaluateTask =
                this.SendRequest(
                    EvaluateRequest.Type,
                    new EvaluateRequestArguments
                    {
                        Expression = choiceScript,
                        Context = "repl"
                    });

            // Wait for the choice prompt event and check expected values
            ShowChoicePromptNotification showChoicePromptEvent = await choicePromptTask;

            Assert.Equal(1, showChoicePromptEvent.DefaultChoice);

            // Prepare to receive script output
            Task<OutputEventBody> outputTask = this.WaitForEvent(OutputEvent.Type);

            // Respond to the prompt event
            await this.SendEvent(
                CompleteChoicePromptNotification.Type,
                new CompleteChoicePromptNotification
                {
                    ChosenItem = "a"
                });

            // Wait for the selection to appear as output
            await evaluateTask;
            OutputEventBody choiceOutput = await outputTask;
            Assert.Equal("0\r\n", choiceOutput.Output);
        }

        private Task<TResult> SendRequest<TParams, TResult>(
            RequestType<TParams, TResult> requestType, 
            TParams requestParams)
        {
            return 
                this.languageServiceClient.SendRequest(
                    requestType, 
                    requestParams);
        }

        private Task SendEvent<TParams>(EventType<TParams> eventType, TParams eventParams)
        {
            return 
                this.languageServiceClient.SendEvent(
                    eventType,
                    eventParams);
        }

        private async Task SendOpenFileEvent(string filePath, bool waitForDiagnostics = true)
        {
            string fileContents = string.Join(Environment.NewLine, File.ReadAllLines(filePath));

            // Start the event waiter for diagnostics before sending the
            // open event to make sure that we catch it
            Task<PublishDiagnosticsNotification> diagnosticWaitTask = null;
            if (waitForDiagnostics)
            {
                // Wait for the diagnostic event
                diagnosticWaitTask = 
                    this.WaitForEvent(
                        PublishDiagnosticsNotification.Type);
            }

            await this.SendEvent(
                DidOpenTextDocumentNotification.Type,
                new DidOpenTextDocumentNotification()
                {
                    Uri = filePath,
                    Text = fileContents
                });

            if (diagnosticWaitTask != null)
            {
                await diagnosticWaitTask;
            }
        }

        private void QueueEventsForType<TParams>(EventType<TParams> eventType)
        {
            var eventQueue =
                this.eventQueuePerType.AddOrUpdate(
                    eventType.MethodName,
                    new AsyncProducerConsumerQueue<object>(),
                    (key, queue) => queue);

            this.languageServiceClient.SetEventHandler(
                eventType,
                (p, ctx) =>
                {
                    return eventQueue.EnqueueAsync(p);   
                });
        }

        private Task<TParams> WaitForEvent<TParams>(EventType<TParams> eventType)
        {
            // Use the event queue if one has been registered
            AsyncProducerConsumerQueue<object> eventQueue = null;
            if (this.eventQueuePerType.TryGetValue(eventType.MethodName, out eventQueue))
            {
                return 
                    eventQueue
                        .DequeueAsync()
                        .ContinueWith<TParams>(
                            task => (TParams)task.Result);
            }
            else
            {
                TaskCompletionSource<TParams> eventTask = new TaskCompletionSource<TParams>();

                this.languageServiceClient.SetEventHandler(
                    eventType,
                    (p, ctx) =>
                    {
                        if (!eventTask.Task.IsCompleted)
                        {
                            eventTask.SetResult(p);
                        }

                        return Task.FromResult(true);
                    },
                    true);  // Override any existing handler

                return eventTask.Task;
            }
        }
    }
}
