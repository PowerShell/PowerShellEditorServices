//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Xunit.Abstractions;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace PowerShellEditorServices.Test.E2E
{
    public class LanguageServerProtocolMessageTests : IClassFixture<LSPTestsFixture>, IDisposable
    {
        private readonly static string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static bool s_registeredOnLogMessage;

        private readonly LanguageClient LanguageClient;
        private readonly List<Diagnostic> Diagnostics;
        private readonly string PwshExe;
        private readonly ITestOutputHelper _output;

        public LanguageServerProtocolMessageTests(ITestOutputHelper output, LSPTestsFixture data)
        {
            Diagnostics = new List<Diagnostic>();
            LanguageClient = data.LanguageClient;
            Diagnostics = data.Diagnostics;
            PwshExe = TestsFixture.PwshExe;
            Diagnostics.Clear();

            _output = output;

            if (!s_registeredOnLogMessage)
            {
                LanguageClient.Window.OnLogMessage((message, messageType) =>
                {
                    _output.WriteLine($"{messageType.ToString()}: {message}");
                });

                s_registeredOnLogMessage = true;
            }
        }

        public void Dispose()
        {
            Diagnostics.Clear();
        }

        private string NewTestFile(string script, bool isPester = false)
        {
            string fileExt = isPester ? ".Tests.ps1" : ".ps1";
            string filePath = Path.Combine(s_binDir, Path.GetRandomFileName() + fileExt);
            File.WriteAllText(filePath, script);

            LanguageClient.SendNotification("textDocument/didOpen", new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    LanguageId = "powershell",
                    Version = 0,
                    Text = script,
                    Uri = new Uri(filePath)
                }
            });

            // Give PSES a chance to run what it needs to run.
            Thread.Sleep(1000);

            return filePath;
        }

        private async Task WaitForDiagnostics()
        {
            // Wait for PSSA to finish.
            int i = 0;
            while(Diagnostics.Count == 0)
            {
                if(i >= 10)
                {
                    throw new InvalidDataException("No diagnostics showed up after 20s.");
                }

                await Task.Delay(2000);
                i++;
            }
        }

        [Fact]
        public async Task CanSendPowerShellGetVersionRequest()
        {
            PowerShellVersion details
                = await LanguageClient.SendRequest<PowerShellVersion>("powerShell/getVersion", new GetVersionParams());

            if(PwshExe == "powershell")
            {
                Assert.Equal("Desktop", details.Edition);
            }
            else
            {
                Assert.Equal("Core", details.Edition);
            }
        }

        [Fact]
        public async Task CanSendWorkspaceSymbolRequest()
        {

            NewTestFile(@"
function CanSendWorkspaceSymbolRequest {
    Write-Host 'hello'
}
");

            SymbolInformationContainer symbols = await LanguageClient.SendRequest<SymbolInformationContainer>(
                "workspace/symbol",
                new WorkspaceSymbolParams
                {
                    Query = "CanSendWorkspaceSymbolRequest"
                });

            SymbolInformation symbol = Assert.Single(symbols);
            Assert.Equal("CanSendWorkspaceSymbolRequest { }", symbol.Name);
        }

        [Fact]
        public async Task CanReceiveDiagnosticsFromFileOpen()
        {
            NewTestFile("$a = 4");
            await WaitForDiagnostics();

            Diagnostic diagnostic = Assert.Single(Diagnostics);
            Assert.Equal("PSUseDeclaredVarsMoreThanAssignments", diagnostic.Code);
        }

        [Fact]
        public async Task CanReceiveDiagnosticsFromConfigurationChange()
        {
            NewTestFile("gci | % { $_ }");
            await WaitForDiagnostics();

            // NewTestFile doesn't clear diagnostic notifications so we need to do that for this test.
            Diagnostics.Clear();

            try
            {
                LanguageClient.SendNotification("workspace/didChangeConfiguration",
                new DidChangeConfigurationParams
                {
                    Settings = JToken.Parse(@"
{
    ""PowerShell"": {
        ""ScriptAnalysis"": {
            ""Enable"": false
        }
    }
}
")
                });

                Assert.Empty(Diagnostics);
            }
            finally
            {
                LanguageClient.SendNotification("workspace/didChangeConfiguration",
                new DidChangeConfigurationParams
                {
                    Settings = JToken.Parse(@"
{
    ""PowerShell"": {
        ""ScriptAnalysis"": {
            ""Enable"": true
        }
    }
}
")
                });
            }
        }

        [Fact]
        public async Task CanSendFoldingRangeRequest()
        {
            string scriptPath = NewTestFile(@"gci | % {
$_

@""
    $_
""@
}");

            Container<FoldingRange> foldingRanges =
                await LanguageClient.SendRequest<Container<FoldingRange>>(
                    "textDocument/foldingRange",
                    new FoldingRangeRequestParam
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(scriptPath)
                        }
                    });

            Assert.Collection(foldingRanges.OrderBy(f => f.StartLine),
                range1 =>
                {
                    Assert.Equal(0, range1.StartLine);
                    Assert.Equal(8, range1.StartCharacter);
                    Assert.Equal(5, range1.EndLine);
                    Assert.Equal(1, range1.EndCharacter);
                },
                range2 =>
                {
                    Assert.Equal(3, range2.StartLine);
                    Assert.Equal(0, range2.StartCharacter);
                    Assert.Equal(4, range2.EndLine);
                    Assert.Equal(2, range2.EndCharacter);
                });
        }

        [Fact]
        public async Task CanSendFormattingRequest()
        {
            string scriptPath = NewTestFile(@"
gci | % {
Get-Process
}

");

            TextEditContainer textEdits = await LanguageClient.SendRequest<TextEditContainer>(
                "textDocument/formatting",
                new DocumentFormattingParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = new Uri(scriptPath)
                    },
                    Options = new FormattingOptions
                    {
                        TabSize = 4,
                        InsertSpaces = false
                    }
                });

            TextEdit textEdit = Assert.Single(textEdits);

            // If we have a tab, formatting ran.
            Assert.Contains("\t", textEdit.NewText);
        }

        [Fact]
        public async Task CanSendRangeFormattingRequest()
        {
            string scriptPath = NewTestFile(@"
gci | % {
Get-Process
}

");

            TextEditContainer textEdits = await LanguageClient.SendRequest<TextEditContainer>(
                "textDocument/formatting",
                new DocumentRangeFormattingParams
                {
                    Range = new Range
                    {
                       Start = new Position
                       {
                           Line = 2,
                           Character = 0
                       },
                       End = new Position
                       {
                           Line = 3,
                           Character = 0
                       }
                    },
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = new Uri(scriptPath)
                    },
                    Options = new FormattingOptions
                    {
                        TabSize = 4,
                        InsertSpaces = false
                    }
                });

            TextEdit textEdit = Assert.Single(textEdits);

            // If we have a tab, formatting ran.
            Assert.Contains("\t", textEdit.NewText);
        }

        [Fact]
        public async Task CanSendDocumentSymbolRequest()
        {
            string scriptPath = NewTestFile(@"
function CanSendDocumentSymbolRequest {

}

CanSendDocumentSymbolRequest
");

            SymbolInformationOrDocumentSymbolContainer symbolInformationOrDocumentSymbols =
                await LanguageClient.SendRequest<SymbolInformationOrDocumentSymbolContainer>(
                    "textDocument/documentSymbol",
                    new DocumentSymbolParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(scriptPath)
                        }
                    });

            Assert.Collection(symbolInformationOrDocumentSymbols,
                symInfoOrDocSym => {
                    Range range = symInfoOrDocSym.SymbolInformation.Location.Range;

                    Assert.Equal(1, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(3, range.End.Line);
                    Assert.Equal(1, range.End.Character);
                });
        }

        [Fact]
        public async Task CanSendReferencesRequest()
        {
            string scriptPath = NewTestFile(@"
function CanSendReferencesRequest {

}

CanSendReferencesRequest
");

            LocationContainer locations = await LanguageClient.SendRequest<LocationContainer>(
                "textDocument/references",
                new ReferenceParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = new Uri(scriptPath)
                    },
                    Position = new Position
                    {
                        Line = 5,
                        Character = 0
                    },
                    Context = new ReferenceContext
                    {
                        IncludeDeclaration = false
                    }
                });

            Assert.Collection(locations,
                location1 =>
                {
                    Range range = location1.Range;
                    Assert.Equal(1, range.Start.Line);
                    Assert.Equal(9, range.Start.Character);
                    Assert.Equal(1, range.End.Line);
                    Assert.Equal(33, range.End.Character);

                },
                location2 =>
                {
                    Range range = location2.Range;
                    Assert.Equal(5, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(5, range.End.Line);
                    Assert.Equal(24, range.End.Character);
                });
        }

        [Fact]
        public async Task CanSendDocumentHighlightRequest()
        {
            string scriptPath = NewTestFile(@"
Write-Host 'Hello!'

Write-Host 'Goodbye'
");

            DocumentHighlightContainer documentHighlights =
                await LanguageClient.SendRequest<DocumentHighlightContainer>(
                    "textDocument/documentHighlight",
                    new DocumentHighlightParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(scriptPath)
                        },
                        Position = new Position
                        {
                            Line = 4,
                            Character = 1
                        }
                    });

            Assert.Collection(documentHighlights,
                documentHighlight1 =>
                {
                    Range range = documentHighlight1.Range;
                    Assert.Equal(1, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(1, range.End.Line);
                    Assert.Equal(10, range.End.Character);

                },
                documentHighlight2 =>
                {
                    Range range = documentHighlight2.Range;
                    Assert.Equal(3, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(3, range.End.Line);
                    Assert.Equal(10, range.End.Character);
                });
        }

        [Fact]
        public async Task CanSendPowerShellGetPSHostProcessesRequest()
        {
            var process = new Process();
            process.StartInfo.FileName = PwshExe;
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NoLogo");
            process.StartInfo.ArgumentList.Add("-NoExit");

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();

            // Wait for the process to start.
            Thread.Sleep(1000);

            PSHostProcessResponse[] pSHostProcessResponses = null;

            try
            {
                pSHostProcessResponses =
                    await LanguageClient.SendRequest<PSHostProcessResponse[]>(
                        "powerShell/getPSHostProcesses",
                        new GetPSHostProcesssesParams { });
            }
            finally
            {
                process.Kill();
                process.Dispose();
            }

            Assert.NotEmpty(pSHostProcessResponses);
        }

        [Fact]
        public async Task CanSendPowerShellGetRunspaceRequest()
        {
            var process = new Process();
            process.StartInfo.FileName = PwshExe;
            process.StartInfo.ArgumentList.Add("-NoProfile");
            process.StartInfo.ArgumentList.Add("-NoLogo");
            process.StartInfo.ArgumentList.Add("-NoExit");

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();

            // Wait for the process to start.
            Thread.Sleep(1000);

            RunspaceResponse[] runspaceResponses = null;
            try
            {
                runspaceResponses =
                    await LanguageClient.SendRequest<RunspaceResponse[]>(
                        "powerShell/getRunspace",
                        new GetRunspaceParams
                        {
                            ProcessId = $"{process.Id}"
                        });
            }
            finally
            {
                process.Kill();
                process.Dispose();
            }

            Assert.NotEmpty(runspaceResponses);
        }

        [Fact]
        public async Task CanSendPesterCodeLensRequest()
        {
            string filePath = NewTestFile(@"
Describe 'DescribeName' {
    Context 'ContextName' {
        It 'ItName' {
            1 | Should - Be 1
        }
    }
}
", isPester: true);

            CodeLensContainer codeLenses = await LanguageClient.SendRequest<CodeLensContainer>(
                "textDocument/codeLens",
                new CodeLensParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = new Uri(filePath)
                    }
                });

            Assert.Collection(codeLenses,
                codeLens1 =>
                {
                    Range range = codeLens1.Range;

                    Assert.Equal(1, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(7, range.End.Line);
                    Assert.Equal(1, range.End.Character);

                    Assert.Equal("Run tests", codeLens1.Command.Title);
                },
                codeLens2 =>
                {
                    Range range = codeLens2.Range;

                    Assert.Equal(1, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(7, range.End.Line);
                    Assert.Equal(1, range.End.Character);

                    Assert.Equal("Debug tests", codeLens2.Command.Title);
                });
        }

        [Fact]
        public async Task CanSendReferencesCodeLensRequest()
        {
            string filePath = NewTestFile(@"
function CanSendReferencesCodeLensRequest {

}

CanSendReferencesCodeLensRequest
");

            CodeLensContainer codeLenses = await LanguageClient.SendRequest<CodeLensContainer>(
                "textDocument/codeLens",
                new CodeLensParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = new Uri(filePath)
                    }
                });

            CodeLens codeLens = Assert.Single(codeLenses);

            Range range = codeLens.Range;
            Assert.Equal(1, range.Start.Line);
            Assert.Equal(0, range.Start.Character);
            Assert.Equal(3, range.End.Line);
            Assert.Equal(1, range.End.Character);

            CodeLens codeLensResolveResult = await LanguageClient.SendRequest<CodeLens>(
                "codeLens/resolve",
                codeLens);

            Assert.Equal("1 reference", codeLensResolveResult.Command.Title);
        }

        [Fact]
        public async Task CanSendCodeActionRequest()
        {
            string filePath = NewTestFile("gci");
            await WaitForDiagnostics();

            CommandOrCodeActionContainer commandOrCodeActions =
                await LanguageClient.SendRequest<CommandOrCodeActionContainer>(
                    "textDocument/codeAction",
                    new CodeActionParams
                    {
                        TextDocument = new TextDocumentIdentifier(
                            new Uri(filePath, UriKind.Absolute)),
                        Range = new Range
                        {
                            Start = new Position
                            {
                                Line = 0,
                                Character = 0
                            },
                            End = new Position
                            {
                                Line = 0,
                                Character = 3
                            }
                        },
                        Context = new CodeActionContext
                        {
                            Diagnostics = new Container<Diagnostic>(Diagnostics)
                        }
                    });

            Assert.Single(commandOrCodeActions,
                command => command.Command.Name == "PowerShell.ApplyCodeActionEdits");
        }

        [Fact]
        public async Task CanSendCompletionAndCompletionResolveRequest()
        {
            string filePath = NewTestFile("Write-H");

            CompletionList completionItems = await LanguageClient.TextDocument.Completions(
                filePath, line: 0, column: 7);

            CompletionItem completionItem = Assert.Single(completionItems,
                completionItem1 => completionItem1.Label == "Write-Host");

            CompletionItem updatedCompletionItem = await LanguageClient.SendRequest<CompletionItem>(
                "completionItem/resolve",
                completionItem);

            Assert.Contains("Writes customized output to a host", updatedCompletionItem.Documentation.String);
        }

        [Fact]
        public async Task CanSendHoverRequest()
        {
            string filePath = NewTestFile("Write-Host");

            Hover hover = await LanguageClient.TextDocument.Hover(filePath, line: 0, column: 1);

            Assert.True(hover.Contents.HasMarkedStrings);
            Assert.Collection(hover.Contents.MarkedStrings,
                str1 =>
                {
                    Assert.Equal("function Write-Host", str1.Value);
                },
                str2 =>
                {
                    Assert.Equal("markdown", str2.Language);
                    Assert.Equal("Writes customized output to a host.", str2.Value);
                });
        }

        [Fact]
        public async Task CanSendSignatureHelpRequest()
        {
            string filePath = NewTestFile("Get-Date ");

            SignatureHelp signatureHelp = await LanguageClient.SendRequest<SignatureHelp>(
                "textDocument/signatureHelp",
                new SignatureHelpParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = new Uri(filePath)
                    },
                    Position = new Position
                    {
                        Line = 0,
                        Character = 9
                    }
                });

            Assert.Contains("Get-Date", signatureHelp.Signatures.First().Label);
        }

        [Fact]
        public async Task CanSendDefinitionRequest()
        {
            string scriptPath = NewTestFile(@"
function CanSendDefinitionRequest {

}

CanSendDefinitionRequest
");

            LocationOrLocationLinks locationOrLocationLinks =
                await LanguageClient.SendRequest<LocationOrLocationLinks>(
                    "textDocument/definition",
                    new DefinitionParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(scriptPath)
                        },
                        Position = new Position
                        {
                            Line = 5,
                            Character = 2
                        }
                    });

            LocationOrLocationLink locationOrLocationLink =
                    Assert.Single(locationOrLocationLinks);

            Assert.Equal(1, locationOrLocationLink.Location.Range.Start.Line);
            Assert.Equal(9, locationOrLocationLink.Location.Range.Start.Character);
            Assert.Equal(1, locationOrLocationLink.Location.Range.End.Line);
            Assert.Equal(33, locationOrLocationLink.Location.Range.End.Character);
        }

        [Fact]
        public async Task CanSendGetProjectTemplatesRequest()
        {
            GetProjectTemplatesResponse getProjectTemplatesResponse =
                await LanguageClient.SendRequest<GetProjectTemplatesResponse>(
                    "powerShell/getProjectTemplates",
                    new GetProjectTemplatesRequest
                    {
                        IncludeInstalledModules = true
                    });

            Assert.Collection(getProjectTemplatesResponse.Templates.OrderBy(t => t.Title),
                template1 =>
                {
                    Assert.Equal("AddPSScriptAnalyzerSettings", template1.Title);
                },
                template2 =>
                {
                    Assert.Equal("New PowerShell Manifest Module", template2.Title);
                });
        }

        [Fact]
        public async Task CanSendGetCommentHelpRequest()
        {
            string scriptPath = NewTestFile(@"
function CanSendGetCommentHelpRequest {
    param(
        [string]
        $myParam
    )
}
");

            CommentHelpRequestResult commentHelpRequestResult =
                await LanguageClient.SendRequest<CommentHelpRequestResult>(
                    "powerShell/getCommentHelp",
                    new CommentHelpRequestParams
                    {
                        DocumentUri = new Uri(scriptPath).ToString(),
                        BlockComment = false,
                        TriggerPosition = new Position
                        {
                            Line = 0,
                            Character = 0
                        }
                    });

            Assert.NotEmpty(commentHelpRequestResult.Content);
            Assert.Contains("myParam", commentHelpRequestResult.Content[7]);
        }

        [Fact]
        public async Task CanSendEvaluateRequest()
        {
            EvaluateResponseBody evaluateResponseBody =
                await LanguageClient.SendRequest<EvaluateResponseBody>(
                    "evaluate",
                    new EvaluateRequestArguments
                    {
                        Expression = "Get-ChildItem"
                    });

            // These always gets returned so this test really just makes sure we get _any_ response.
            Assert.Equal("", evaluateResponseBody.Result);
            Assert.Equal(0, evaluateResponseBody.VariablesReference);
        }

        [Fact]
        public async Task CanSendGetCommandRequest()
        {
            List<PSCommandMessage> pSCommandMessages =
                await LanguageClient.SendRequest<List<PSCommandMessage>>("powerShell/getCommand", new GetCommandParams());

            Assert.NotEmpty(pSCommandMessages);
            // There should be at least 20 commands or so.
            Assert.True(pSCommandMessages.Count > 20);
        }

        [Fact]
        public async Task CanSendExpandAliasRequest()
        {
            ExpandAliasResult expandAliasResult =
                await LanguageClient.SendRequest<ExpandAliasResult>(
                    "powerShell/expandAlias",
                    new ExpandAliasParams
                    {
                        Text = "gci"
                    }
                );

            Assert.Equal("Get-ChildItem", expandAliasResult.Text);
        }
    }
}
