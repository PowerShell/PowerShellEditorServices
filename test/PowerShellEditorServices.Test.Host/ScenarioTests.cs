//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class ScenarioTests : IDisposable
    {
        private int messageId = 0;

        private LanguageServiceManager languageServiceManager = 
            new LanguageServiceManager();

        private MessageReader MessageReader 
        { 
            get { return this.languageServiceManager.MessageReader; } 
        }

        private MessageWriter MessageWriter
        {
            get { return this.languageServiceManager.MessageWriter; }
        }

        public ScenarioTests()
        {
            this.languageServiceManager.Start();
        }

        public void Dispose()
        {
            this.languageServiceManager.Stop();
        }

        [Fact]
        public async Task ServiceReturnsSyntaxErrors()
        {
            // Send the 'didOpen' event
            await this.SendOpenFileEvent("TestFiles\\SimpleSyntaxError.ps1", false);

            // Wait for the diagnostic event
            PublishDiagnosticsNotification diagnostics = this.WaitForEvent(PublishDiagnosticsNotification.Type);

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

        [Fact(Skip="Skipped until variable documentation gathering is added back.")]
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
            await this.SendRequestWithoutWait(
                EvaluateRequest.Type,
                new EvaluateRequestArguments
                {
                    Expression = "1 + 2"
                });

            OutputEventBody outputEvent = this.WaitForEvent(OutputEvent.Type);
            this.WaitForResponse(EvaluateRequest.Type, this.messageId);

            Assert.Equal("3\r\n", outputEvent.Output);
            Assert.Equal("stdout", outputEvent.Category);
        }

        [Fact]//(Skip = "Choice prompt functionality is currently in transition to a new model.")]
        public async Task ServiceExecutesReplCommandAndReceivesChoicePrompt()
        {
            // TODO: This test is removed until a new choice prompt strategy is determined.

//            string choiceScript =
//                @"
//                $caption = ""Test Choice"";
//                $message = ""Make a selection"";
//                $choiceA = new-Object System.Management.Automation.Host.ChoiceDescription ""&A"",""A"";
//                $choiceB = new-Object System.Management.Automation.Host.ChoiceDescription ""&B"",""B"";
//                $choices = [System.Management.Automation.Host.ChoiceDescription[]]($choiceA,$choiceB);
//                $response = $host.ui.PromptForChoice($caption, $message, $choices, 1)
//                $response";

//            await this.MessageWriter.WriteMessage(
//                new ReplExecuteRequest
//                {
//                    Arguments = new ReplExecuteArgs
//                    {
//                        CommandString = choiceScript
//                    }
//                });

//            // Wait for the choice prompt event and check expected values
//            ReplPromptChoiceEvent replPromptChoiceEvent = this.WaitForMessage<ReplPromptChoiceEvent>();
//            Assert.Equal(1, replPromptChoiceEvent.Body.DefaultChoice);

//            // Respond to the prompt event
//            await this.MessageWriter.WriteMessage(
//                new ReplPromptChoiceResponse
//                {
//                    Body = new ReplPromptChoiceResponseBody
//                    {
//                        Choice = 0
//                    }
//                });

//            // Wait for the selection to appear as output
//            ReplWriteOutputEvent replWriteLineEvent = this.WaitForMessage<ReplWriteOutputEvent>();
//            Assert.Equal("0", replWriteLineEvent.Body.LineContents);
        }

        private async Task<TResult> SendRequest<TParams, TResult, TError>(
            RequestType<TParams, TResult, TError> requestType, 
            TParams requestParams)
        {
            await this.SendRequestWithoutWait(requestType, requestParams);
            return this.WaitForResponse(requestType, this.messageId);
        }

        private async Task SendRequestWithoutWait<TParams, TResult, TError>(
            RequestType<TParams, TResult, TError> requestType,
            TParams requestParams)
        {
            this.messageId++;

            await this.MessageWriter.WriteMessage(
                Message.Request(
                    this.messageId.ToString(),
                    requestType.TypeName,
                    JToken.FromObject(requestParams)));
       }

        private async Task SendEvent<TParams>(EventType<TParams> eventType, TParams eventParams)
        {
            await this.MessageWriter.WriteMessage(
                Message.Event(
                    eventType.MethodName,
                    JToken.FromObject(eventParams)));
        }

        private async Task SendOpenFileEvent(string filePath, bool waitForDiagnostics = true)
        {
            string fileContents = string.Join(Environment.NewLine, File.ReadAllLines(filePath));

            await this.SendEvent(
                DidOpenTextDocumentNotification.Type, 
                new DidOpenTextDocumentNotification() 
                { 
                    Uri = filePath,
                    Text = fileContents
                });

            if (waitForDiagnostics)
            {
                // Wait for the diagnostic event
                this.WaitForEvent(PublishDiagnosticsNotification.Type);
            }
        }

        private TParams WaitForEvent<TParams>(EventType<TParams> eventType)
        {
            // TODO: Integrate timeout!
            Message receivedMessage = this.MessageReader.ReadMessage().Result;

            Assert.Equal(MessageType.Event, receivedMessage.MessageType);
            Assert.Equal(eventType.MethodName, receivedMessage.Method);

            return
                receivedMessage.Contents != null ?
                    receivedMessage.Contents.ToObject<TParams>() :
                    default(TParams);
        }

        private TResult WaitForResponse<TParams, TResult, TError>(
            RequestType<TParams, TResult, TError> requestType, 
            int expectedId)
        {
            // TODO: Integrate timeout!
            Message receivedMessage = this.MessageReader.ReadMessage().Result;

            Assert.Equal(MessageType.Response, receivedMessage.MessageType);
            Assert.Equal(expectedId.ToString(), receivedMessage.Id);

            return
                receivedMessage.Contents != null ?
                    receivedMessage.Contents.ToObject<TResult>() :
                    default(TResult);
        }
    }
}
