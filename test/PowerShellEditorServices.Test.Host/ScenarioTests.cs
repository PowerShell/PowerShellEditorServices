//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Transport.Stdio;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Event;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Message;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Request;
using Microsoft.PowerShell.EditorServices.Transport.Stdio.Response;
using System;
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
            DiagnosticEvent syntaxEvent = this.WaitForMessage<DiagnosticEvent>();
            DiagnosticEvent semanticEvent = this.WaitForMessage<DiagnosticEvent>();

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
                new OpenFileRequest(fileName));
        }

        private void SendErrorRequest(params string[] fileNames)
        {
            this.MessageWriter.WriteMessage(
                new ErrorRequest(fileNames));
        }

        private TMessage WaitForMessage<TMessage>() where TMessage : MessageBase
        {
            // TODO: Integrate timeout!
            MessageBase receivedMessage = this.MessageReader.ReadMessage().Result;
            return Assert.IsType<TMessage>(receivedMessage);
        }
    }
}
