﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.Client;
using Microsoft.PowerShell.EditorServices.Protocol.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Channel;
using Microsoft.PowerShell.EditorServices.Protocol.Messages;
using Microsoft.PowerShell.EditorServices.Protocol.Server;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Host
{
    public class LanguageServerTests : ServerTestsBase, IAsyncLifetime
    {
        private ILogger logger;
        private LanguageServiceClient languageServiceClient;

        public async Task InitializeAsync()
        {
            string testLogPath =
                Path.Combine(
#if CoreCLR
                    AppContext.BaseDirectory,
#else
                    AppDomain.CurrentDomain.BaseDirectory,
#endif
                    "logs",
                    this.GetType().Name,
                    Guid.NewGuid().ToString().Substring(0, 8));

            this.logger =
                new FileLogger(
                    testLogPath + "-client.log",
                    LogLevel.Verbose);

            testLogPath += "-server.log";
            System.Console.WriteLine("        Output log at path: {0}", testLogPath);

            Tuple<int, int> portNumbers =
                await this.LaunchService(
                    testLogPath,
                    waitForDebugger: false);
                    //waitForDebugger: true);

            this.languageServiceClient =
                new LanguageServiceClient(
                    await TcpSocketClientChannel.Connect(
                        portNumbers.Item1,
                        MessageProtocolType.LanguageServer,
                        this.logger),
                    this.logger);

            this.messageSender = this.languageServiceClient;
            this.messageHandlers = this.languageServiceClient;

            await this.languageServiceClient.Start();
        }

        public async Task DisposeAsync()
        {
            await this.languageServiceClient.Stop();

            this.KillService();
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

        [Fact(Skip = "Skipping until Script Analyzer integration is added back")]
        public async Task ServiceReturnsSemanticMarkers()
        {
            // Send the 'didOpen' event
            await this.SendOpenFileEvent("TestFiles\\SimpleSemanticError.ps1", false);

            // Wait for the diagnostic event
            PublishDiagnosticsNotification diagnostics =
                await this.WaitForEvent(
                    PublishDiagnosticsNotification.Type);

            // Was there a semantic error?
            Assert.NotEqual(0, diagnostics.Diagnostics.Length);
            Assert.Contains("unapproved", diagnostics.Diagnostics[0].Message);
        }

        [Fact]
        public async Task ServiceReturnsNoErrorsForUsingRelativeModulePaths()
        {
            // Send the 'didOpen' event
            await this.SendOpenFileEvent("TestFiles\\Module.psm1", false);

            // Wait for the diagnostic event
            PublishDiagnosticsNotification diagnostics =
                await this.WaitForEvent(
                    PublishDiagnosticsNotification.Type);

            // Was there a syntax error?
            Assert.Equal(0, diagnostics.Diagnostics.Length);
        }

        [Fact]
        public async Task ServiceCompletesFunctionName()
        {
            await this.SendOpenFileEvent("TestFiles\\CompleteFunctionName.ps1");

            CompletionItem[] completions =
                await this.SendRequest(
                    CompletionRequest.Type,
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\CompleteFunctionName.ps1",
                        },
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
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\CompleteFunctionName.ps1"
                        },
                        Position = new Position
                        {
                            Line = 3,
                            Character = 5
                        }
                    });

            CompletionItem consoleFileNameItem =
                completions
                    .FirstOrDefault(
                        c => c.Label == "ConsoleFileName");

            Assert.NotNull(consoleFileNameItem);
            Assert.Equal("[string]", consoleFileNameItem.Detail);
        }

        [Fact(Skip = "Skipped until variable documentation gathering is added back.")]
        public async Task CompletesDetailOnVariableDocSuggestion()
        {
            await this.SendOpenFileEvent("TestFiles\\CompleteFunctionName.ps1");

            await this.SendRequest(
                CompletionRequest.Type,
                new TextDocumentPositionParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = "TestFiles\\CompleteFunctionName.ps1"
                    },
                    Position = new Position
                    {
                        Line = 7,
                        Character = 5
                    }
                });

            // TODO: This section needs to be updated, seems that
            // CompletionsResponse is missing.

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
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\CompleteFunctionName.ps1"
                        },
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
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = "TestFiles\\FindReferences.ps1"
                    },
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
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
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
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
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
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
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
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1",
                        },
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
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
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
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
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
        public async Task FindsDefinitionOfVariableInOtherFile()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
                await this.SendRequest(
                    DefinitionRequest.Type,
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
                        Position = new Position
                        {
                            Line = 15,
                            Character = 20,
                        }
                    });

            Assert.NotNull(locations);
            Assert.Equal(1, locations.Length);
            Assert.EndsWith("VariableDefinition.ps1", locations[0].Uri);
            Assert.Equal(0, locations[0].Range.Start.Line);
            Assert.Equal(0, locations[0].Range.Start.Character);
            Assert.Equal(0, locations[0].Range.End.Line);
            Assert.Equal(20, locations[0].Range.End.Character);
        }

        [Fact]
        public async Task FindDefinitionOfVariableWithSpecialChars()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            Location[] locations =
                await this.SendRequest(
                    DefinitionRequest.Type,
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
                        Position = new Position
                        {
                            Line = 18,
                            Character = 24,
                        }
                    });

            Assert.NotNull(locations);
            Assert.Equal(1, locations.Length);
            Assert.EndsWith("FindReferences.ps1", locations[0].Uri);
            Assert.Equal(17, locations[0].Range.Start.Line);
            Assert.Equal(0, locations[0].Range.Start.Character);
            Assert.Equal(17, locations[0].Range.End.Line);
            Assert.Equal(27, locations[0].Range.End.Character);
        }

        [Fact]
        public async Task FindsOccurencesOnFunctionDefinition()
        {
            await this.SendOpenFileEvent("TestFiles\\FindReferences.ps1");

            DocumentHighlight[] highlights =
                await this.SendRequest(
                    DocumentHighlightRequest.Type,
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
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
                    new TextDocumentPositionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = "TestFiles\\FindReferences.ps1"
                        },
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
            OutputReader outputReader = new OutputReader(this.messageHandlers);

            await
                this.SendRequest(
                    EvaluateRequest.Type,
                    new EvaluateRequestArguments
                    {
                        Expression = "1 + 2"
                    });

            Assert.Equal("1 + 2", await outputReader.ReadLine());
            await outputReader.ReadLine(); // Skip the empty line
            Assert.Equal("3", await outputReader.ReadLine());
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
            OutputReader outputReader = new OutputReader(this.messageHandlers);

            string choiceScript =
                @"
                $caption = ""Test Choice"";
                $message = ""Make a selection"";
                $choiceA = New-Object System.Management.Automation.Host.ChoiceDescription ""&Apple"",""Help for Apple"";
                $choiceB = New-Object System.Management.Automation.Host.ChoiceDescription ""Banana"",""Help for Banana"";
                $choices = [System.Management.Automation.Host.ChoiceDescription[]]($choiceA,$choiceB);
                $host.ui.PromptForChoice($caption, $message, $choices, 1)";

            Task<Tuple<ShowChoicePromptRequest, RequestContext<ShowChoicePromptResponse>>> choicePromptTask =
                this.WaitForRequest(ShowChoicePromptRequest.Type);

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

            // Wait for the choice prompt request and check expected values
            Tuple<ShowChoicePromptRequest, RequestContext<ShowChoicePromptResponse>> requestResponseContext = await choicePromptTask;
            ShowChoicePromptRequest showChoicePromptRequest = requestResponseContext.Item1;
            RequestContext<ShowChoicePromptResponse> requestContext = requestResponseContext.Item2;

            Assert.Equal(1, showChoicePromptRequest.DefaultChoices[0]);

            // Respond to the prompt request
            await requestContext.SendResult(
                new ShowChoicePromptResponse
                {
                    ResponseText = "a"
                });

            // Skip the initial script and prompt lines (6 script lines plus 2 blank lines and 3 prompt lines)
            string[] outputLines = await outputReader.ReadLines(11);

            // Wait for the selection to appear as output
            await evaluateTask;
            Assert.Equal("0", await outputReader.ReadLine());
        }

        [Fact]
        public async Task ServiceExecutesReplCommandAndReceivesInputPrompt()
        {
            OutputReader outputReader = new OutputReader(this.messageHandlers);

            string promptScript =
                @"
                $NameField = New-Object System.Management.Automation.Host.FieldDescription ""Name""
                $NameField.SetParameterType([System.String])
                $fields = [System.Management.Automation.Host.FieldDescription[]]($NameField)
                $host.ui.Prompt($null, $null, $fields)";

            Task<Tuple<ShowInputPromptRequest, RequestContext<ShowInputPromptResponse>>> inputPromptTask =
                this.WaitForRequest(ShowInputPromptRequest.Type);

            // Execute the script but don't await the task yet because
            // the choice prompt will block execution from completing
            Task<EvaluateResponseBody> evaluateTask =
                this.SendRequest(
                    EvaluateRequest.Type,
                    new EvaluateRequestArguments
                    {
                        Expression = promptScript,
                        Context = "repl"
                    });

            // Wait for the input prompt request and check expected values
            Tuple<ShowInputPromptRequest, RequestContext<ShowInputPromptResponse>> requestResponseContext = await inputPromptTask;
            ShowInputPromptRequest showInputPromptRequest = requestResponseContext.Item1;
            RequestContext<ShowInputPromptResponse> requestContext = requestResponseContext.Item2;

            Assert.Equal("Name", showInputPromptRequest.Name);

            // Respond to the prompt request
            await requestContext.SendResult(
                new ShowInputPromptResponse
                {
                    ResponseText = "John"
                });

            // Skip the initial script lines (4 script lines plus 2 blank lines)
            string[] scriptLines = await outputReader.ReadLines(6);

            // Verify the first line
            Assert.Equal("Name: John", await outputReader.ReadLine());

            // Verify the rest of the output
            string[] outputLines = await outputReader.ReadLines(4);
            Assert.Equal("", outputLines[0]);
            Assert.Equal("Key  Value", outputLines[1]);
            Assert.Equal("---  -----", outputLines[2]);
            Assert.Equal("Name John ", outputLines[3]);

            // Wait for execution to complete
            await evaluateTask;
        }

        [Fact(Skip = "Native command output in the legacy host has been disabled for now, may re-enable later")]
        public async Task ServiceExecutesNativeCommandAndReceivesCommand()
        {
            OutputReader outputReader = new OutputReader(this.messageHandlers);

            // Execute the script but don't await the task yet because
            // the choice prompt will block execution from completing
            Task<EvaluateResponseBody> evaluateTask =
                this.SendRequest(
                    EvaluateRequest.Type,
                    new EvaluateRequestArguments
                    {
                        Expression = "cmd.exe /c 'echo Test Output'",
                        Context = "repl"
                    });

            // Skip the command line and the following newline
            await outputReader.ReadLines(2);

            // Wait for the selection to appear as output
            await evaluateTask;
            Assert.Equal("Test Output", await outputReader.ReadLine());
        }

        [Fact]
        public async Task ServiceLoadsProfilesOnDemand()
        {
            string testHostName = "Test.PowerShellEditorServices";
            string profileName =
                string.Format(
                    "{0}_{1}",
                    testHostName,
                    ProfilePaths.AllHostsProfileName);
            string testProfilePath =
                Path.Combine(
                    Path.GetFullPath(
                        @"..\..\..\..\PowerShellEditorServices.Test.Shared\Profile\"),
                    profileName);

            string currentUserCurrentHostPath =
                Path.Combine(
#if !CoreCLR
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "WindowsPowerShell",
#else
                    // TODO: This will need to be improved once we are running tests on CoreCLR
                    "~/.powershell",
#endif
                    profileName);

            // Copy the test profile to the current user's host profile path
            File.Copy(testProfilePath, currentUserCurrentHostPath, true);

            Assert.True(
                File.Exists(currentUserCurrentHostPath),
                "Copied profile path does not exist!");

            OutputReader outputReader = new OutputReader(this.messageHandlers);

            // Send the configuration change to cause profiles to be loaded
            await this.languageServiceClient.SendEvent(
                DidChangeConfigurationNotification<LanguageServerSettingsWrapper>.Type,
                new DidChangeConfigurationParams<LanguageServerSettingsWrapper>
                {
                    Settings = new LanguageServerSettingsWrapper
                    {
                        Powershell = new LanguageServerSettings
                        {
                            EnableProfileLoading = true,
                            ScriptAnalysis = new ScriptAnalysisSettings
                            {
                                Enable = false
                            }
                        }
                    }
                });

            // Wait for the prompt to be written once the profile loads
            Assert.StartsWith("PS ", await outputReader.ReadLine(waitForNewLine: false));

            Task<EvaluateResponseBody> evaluateTask =
                this.SendRequest(
                    EvaluateRequest.Type,
                    new EvaluateRequestArguments
                    {
                        Expression = "\"PROFILE: $(Assert-ProfileLoaded)\"",
                        Context = "repl"
                    });

            // Try reading up to 10 lines to find the expected output line
            string outputString = null;
            for (int i = 0; i < 10; i++)
            {
                outputString = await outputReader.ReadLine();

                if (outputString.StartsWith("PROFILE"))
                {
                    break;
                }
            }

            // Delete the test profile before any assert failures
            // cause the function to exit
            File.Delete(currentUserCurrentHostPath);

            // Wait for the selection to appear as output
            await evaluateTask;
            Assert.Equal("PROFILE: True", outputString);
        }

        [Fact]
        public async Task ServiceReturnsPowerShellVersionDetails()
        {
            PowerShellVersion versionDetails =
                await this.SendRequest(
                    PowerShellVersionRequest.Type,
                    new PowerShellVersionRequest());

            // TODO: This should be more robust and predictable.
            Assert.StartsWith("5.", versionDetails.Version);
            Assert.StartsWith("5.", versionDetails.DisplayVersion);
            Assert.Equal("Desktop", versionDetails.Edition);
            Assert.Equal("x86", versionDetails.Architecture);
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
                new DidOpenTextDocumentParams
                {
                    TextDocument = new TextDocumentItem
                    {
                        Uri = filePath,
                        Text = fileContents,
                        LanguageId = "PowerShell",
                        Version = 0
                    }
                });

            if (diagnosticWaitTask != null)
            {
                await diagnosticWaitTask;
            }
        }
    }
}
