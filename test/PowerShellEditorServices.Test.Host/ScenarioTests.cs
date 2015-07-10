//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Transport.Stdio;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Request;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class ScenarioTests : IDisposable
    {
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
        public void ServiceReturnsSyntaxErrors()
        {
            // Send the 'open' and 'geterr' events
            this.SendOpenFileRequest("TestFiles\\SimpleSyntaxError.ps1");
            this.SendErrorRequest("TestFiles\\SimpleSyntaxError.ps1");

            // Wait for the events
            SyntaxDiagnosticEvent syntaxEvent = this.WaitForMessage<SyntaxDiagnosticEvent>();
            SemanticDiagnosticEvent semanticEvent = this.WaitForMessage<SemanticDiagnosticEvent>();

            // Check for the expected event types
            Assert.Equal("syntaxDiag", syntaxEvent.EventType);
            Assert.Equal("semanticDiag", semanticEvent.EventType);

            // Was there a syntax error?
            Assert.NotEqual(0, syntaxEvent.Body.Diagnostics.Length);
            Assert.False(
                string.IsNullOrEmpty(syntaxEvent.Body.Diagnostics[0].Text));
        }

        [Fact]
        public void ServiceCompletesFunctionName()
        {
            this.SendOpenFileRequest("TestFiles\\CompleteFunctionName.ps1");
            this.MessageWriter.WriteMessage(
                new CompletionsRequest
                {
                    Arguments = new CompletionsRequestArgs
                    {
                        File = "TestFiles\\CompleteFunctionName.ps1",
                        Line = 5,
                        Offset = 4,
                        Prefix = ""
                    }
                });

            CompletionsResponse completions = this.WaitForMessage<CompletionsResponse>();
            Assert.NotNull(completions);
            Assert.NotEqual(completions.Body.Length, 0);

            // TODO: Add more asserts
        }

        [Fact]
        public void CompletesDetailOnVariableSuggestion()
        {
            this.SendOpenFileRequest("TestFiles\\CompleteFunctionName.ps1");
            this.MessageWriter.WriteMessage(
                new CompletionsRequest
                {
                    Arguments = new CompletionsRequestArgs
                    {
                        File = "TestFiles\\CompleteFunctionName.ps1",
                        Line = 4,
                        Offset = 6,
                        Prefix = ""
                    }
                });
            CompletionsResponse completion = this.WaitForMessage<CompletionsResponse>();
            List<string> entryName = new List<string>();
            entryName.Add("$ConsoleFileName");
            this.MessageWriter.WriteMessage(
                new CompletionDetailsRequest
                {
                    Arguments = new CompletionDetailsRequestArgs
                    {
                        File = "TestFiles\\CompleteFunctionName.ps1",
                        Line = 4,
                        Offset = 6,
                        EntryNames = entryName.ToArray()
                    }
                });
            CompletionDetailsResponse completionDetail = this.WaitForMessage<CompletionDetailsResponse>();
            Assert.NotNull(completionDetail.Body[0]);
            Assert.Equal("string", completionDetail.Body[0].Name);
        }

        [Fact]
        public void CompletesDetailOnVariableDocSuggestion()
        {
            this.SendOpenFileRequest("TestFiles\\CompleteFunctionName.ps1");
            this.MessageWriter.WriteMessage(
                new CompletionsRequest
                {
                    Arguments = new CompletionsRequestArgs
                    {
                        File = "TestFiles\\CompleteFunctionName.ps1",
                        Line = 7,
                        Offset = 5,
                        Prefix = ""
                    }
                });
            CompletionsResponse completion = this.WaitForMessage<CompletionsResponse>();
            List<string> entryName = new List<string>();
            entryName.Add("$HKCU:");
            this.MessageWriter.WriteMessage(
                new CompletionDetailsRequest
                {
                    Arguments = new CompletionDetailsRequestArgs
                    {
                        File = "TestFiles\\CompleteFunctionName.ps1",
                        Line = 7,
                        Offset = 5,
                        EntryNames = entryName.ToArray()
                    }
                });
            CompletionDetailsResponse completionDetail = this.WaitForMessage<CompletionDetailsResponse>();
            Assert.NotNull(completionDetail.Body[0]);
            Assert.Equal("The software settings for the current user", completionDetail.Body[0].DocString);
        }

        [Fact]
        public void CompletesDetailOnCommandSuggestion()
        {
            this.SendOpenFileRequest("TestFiles\\CompleteFunctionName.ps1");
            this.MessageWriter.WriteMessage(
                new CompletionsRequest
                {
                    Arguments = new CompletionsRequestArgs
                    {
                        File = "TestFiles\\CompleteFunctionName.ps1",
                        Line = 6,
                        Offset = 9,
                        Prefix = ""
                    }
                });
            CompletionsResponse completion = this.WaitForMessage<CompletionsResponse>();
            List<string> entryName = new List<string>();
            entryName.Add("Get-Process");
            this.MessageWriter.WriteMessage(
                new CompletionDetailsRequest
                {
                    Arguments = new CompletionDetailsRequestArgs
                    {
                        File = "TestFiles\\CompleteFunctionName.ps1",
                        Line = 6,
                        Offset = 9,
                        EntryNames = entryName.ToArray()
                    }
                });
            CompletionDetailsResponse completionDetail = this.WaitForMessage<CompletionDetailsResponse>();
            Assert.Null(completionDetail.Body[0].Name);
        }

        [Fact]
        public void FindsReferencesOfVariable()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new ReferencesRequest
                {
                    Arguments = new FileLocationRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 8,
                        Offset = 5,
                    }
                });

            ReferencesResponse references = this.WaitForMessage<ReferencesResponse>();
            Assert.NotNull(references);
            Assert.Equal(references.Body.Refs.Length, 3);
            Assert.Equal(references.Body.Refs[0].LineText, "$things = 4");
        }

        [Fact]
        public void FindsNoReferencesOfEmptyLine()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new ReferencesRequest
                {
                    Arguments = new FileLocationRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 9,
                        Offset = 1,
                    }
                });

            ReferencesResponse references = this.WaitForMessage<ReferencesResponse>();
            Assert.Null(references.Body);
        }

        [Fact]
        public void FindsReferencesOnFunctionDefinition()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new ReferencesRequest
                {
                    Arguments = new FileLocationRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 1,
                        Offset = 18,
                    }
                });

            ReferencesResponse references = this.WaitForMessage<ReferencesResponse>();
            Assert.NotNull(references);
            Assert.Equal(references.Body.Refs.Length, 3);
            Assert.Equal(references.Body.SymbolName, "My-Function");
        }

        [Fact]
        public void FindsReferencesOnCommand()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new ReferencesRequest
                {
                    Arguments = new FileLocationRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 10,
                        Offset = 2,
                    }
                });

            ReferencesResponse references = this.WaitForMessage<ReferencesResponse>();
            Assert.NotNull(references);
            Assert.Equal(references.Body.Refs.Length, 3);
            Assert.Equal(references.Body.SymbolName, "My-Function");
        }

        [Fact]
        public void FindsDefinitionOfCommand()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new DeclarationRequest
                {
                    Arguments = new FileLocationRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 3,
                        Offset = 12,
                    }
                });
            DefinitionResponse definition = this.WaitForMessage<DefinitionResponse>();
            Assert.NotNull(definition);
            Assert.Equal(1, definition.Body[0].Start.Line);
            Assert.Equal(10, definition.Body[0].Start.Offset);
        }

        [Fact]
        public void FindsNoDefinitionOfBuiltinCommand()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new DeclarationRequest
                {
                    Arguments = new FileLocationRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 12,
                        Offset = 10,
                    }
                });
            DefinitionResponse definition = this.WaitForMessage<DefinitionResponse>();
            Assert.Null(definition.Body);
        }

        [Fact]
        public void FindsDefintionOfVariable()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new DeclarationRequest
                {
                    Arguments = new FileLocationRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 10,
                        Offset = 14,
                    }
                });
            DefinitionResponse definition = this.WaitForMessage<DefinitionResponse>();
            Assert.NotNull(definition);
            Assert.Equal(6, definition.Body[0].Start.Line);
            Assert.Equal(1, definition.Body[0].Start.Offset);
            Assert.Equal(6, definition.Body[0].End.Line);
            Assert.Equal(8, definition.Body[0].End.Offset);
        }

        [Fact]
        public void FindsOccurencesOnFunctionDefinition()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new OccurrencesRequest
                {
                    Arguments = new FileLocationRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 1,
                        Offset = 18,
                    }
                });

            OccurrencesResponse occurences = this.WaitForMessage<OccurrencesResponse>();
            Assert.NotNull(occurences);
            Assert.Equal(occurences.Body.Length, 3);
            Assert.Equal(occurences.Body[1].Start.Line, 3);
        }

        [Fact]
        public void GetsParameterHintsOnCommand()
        {
            this.SendOpenFileRequest("TestFiles\\FindReferences.ps1");
            this.MessageWriter.WriteMessage(
                new SignatureHelpRequest
                {
                    Arguments = new SignatureHelpRequestArgs
                    {
                        File = "TestFiles\\FindReferences.ps1",
                        Line = 14,
                        Offset = 15,
                    }
                });

            SignatureHelpResponse sigHelp = this.WaitForMessage<SignatureHelpResponse>();
            Assert.NotNull(sigHelp);
            Assert.Equal(sigHelp.Body.CommandName, "Write-Output");
            Assert.Equal(sigHelp.Body.ArgumentCount, 1);
        }

        [Fact]
        public void ServiceExecutesReplCommandAndReceivesOutput()
        {
            this.MessageWriter.WriteMessage(
                new ReplExecuteRequest
                {
                    Arguments = new ReplExecuteArgs
                    {
                        CommandString = "1 + 2"
                    }
                });

            ReplWriteOutputEvent replWriteLineEvent = this.WaitForMessage<ReplWriteOutputEvent>();
            Assert.Equal("3", replWriteLineEvent.Body.LineContents);
        }

        [Fact]
        public void ServiceExecutesReplCommandAndReceivesChoicePrompt()
        {
            string choiceScript =
                @"
                $caption = ""Test Choice"";
                $message = ""Make a selection"";
                $choiceA = new-Object System.Management.Automation.Host.ChoiceDescription ""&A"",""A"";
                $choiceB = new-Object System.Management.Automation.Host.ChoiceDescription ""&B"",""B"";
                $choices = [System.Management.Automation.Host.ChoiceDescription[]]($choiceA,$choiceB);
                $response = $host.ui.PromptForChoice($caption, $message, $choices, 1)
                $response";

            this.MessageWriter.WriteMessage(
                new ReplExecuteRequest
                {
                    Arguments = new ReplExecuteArgs
                    {
                        CommandString = choiceScript
                    }
                });

            // Wait for the choice prompt event and check expected values
            ReplPromptChoiceEvent replPromptChoiceEvent = this.WaitForMessage<ReplPromptChoiceEvent>();
            Assert.Equal(1, replPromptChoiceEvent.Body.DefaultChoice);

            // Respond to the prompt event
            this.MessageWriter.WriteMessage(
                new ReplPromptChoiceResponse
                {
                    Body = new ReplPromptChoiceResponseBody
                    {
                        Choice = 0
                    }
                });

            // Wait for the selection to appear as output
            ReplWriteOutputEvent replWriteLineEvent = this.WaitForMessage<ReplWriteOutputEvent>();
            Assert.Equal("0", replWriteLineEvent.Body.LineContents);
        }

        private void SendOpenFileRequest(string fileName)
        {
            this.MessageWriter.WriteMessage(
                OpenFileRequest.Create(fileName));
        }

        private void SendErrorRequest(params string[] fileNames)
        {
            this.MessageWriter.WriteMessage(
                ErrorRequest.Create(fileNames));
        }

        private TMessage WaitForMessage<TMessage>() where TMessage : MessageBase
        {
            // TODO: Integrate timeout!
            MessageBase receivedMessage = this.MessageReader.ReadMessage().Result;
            return Assert.IsType<TMessage>(receivedMessage);
        }
    }
}
