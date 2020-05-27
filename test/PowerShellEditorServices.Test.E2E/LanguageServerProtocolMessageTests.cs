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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
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

        private readonly ILanguageClient PsesLanguageClient;
        private readonly List<Diagnostic> Diagnostics;
        private readonly string PwshExe;

        public LanguageServerProtocolMessageTests(ITestOutputHelper output, LSPTestsFixture data)
        {
            data.Output = output;
            PsesLanguageClient = data.PsesLanguageClient;
            Diagnostics = data.Diagnostics;
            Diagnostics.Clear();

            PwshExe = TestsFixture.PwshExe;
        }

        public void Dispose()
        {
            Diagnostics.Clear();
        }

        private string NewTestFile(string script, bool isPester = false, string languageId = "powershell")
        {
            string fileExt = isPester ? ".Tests.ps1" : ".ps1";
            string filePath = Path.Combine(s_binDir, Path.GetRandomFileName() + fileExt);
            File.WriteAllText(filePath, script);

            PsesLanguageClient.SendNotification("textDocument/didOpen", new DidOpenTextDocumentParams
            {
                TextDocument = new TextDocumentItem
                {
                    LanguageId = languageId,
                    Version = 0,
                    Text = script,
                    Uri = new Uri(filePath)
                }
            });

            // Give PSES a chance to run what it needs to run.
            Thread.Sleep(2000);

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
                = await PsesLanguageClient
                    .SendRequest<GetVersionParams>("powerShell/getVersion", new GetVersionParams())
                    .Returning<PowerShellVersion>(CancellationToken.None);

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

            Container<SymbolInformation> symbols = await PsesLanguageClient
                .SendRequest<WorkspaceSymbolParams>(
                    "workspace/symbol",
                    new WorkspaceSymbolParams
                    {
                        Query = "CanSendWorkspaceSymbolRequest"
                    })
                .Returning<Container<SymbolInformation>>(CancellationToken.None);

            SymbolInformation symbol = Assert.Single(symbols);
            Assert.Equal("CanSendWorkspaceSymbolRequest { }", symbol.Name);
        }

        [SkippableFact]
        public async Task CanReceiveDiagnosticsFromFileOpen()
        {
            Skip.If(
                TestsFixture.RunningInConstainedLanguageMode && TestsFixture.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            NewTestFile("$a = 4");
            await WaitForDiagnostics();

            Diagnostic diagnostic = Assert.Single(Diagnostics);
            Assert.Equal("PSUseDeclaredVarsMoreThanAssignments", diagnostic.Code);
        }

        [Fact]
        public async Task WontReceiveDiagnosticsFromFileOpenThatIsNotPowerShell()
        {
            NewTestFile("$a = 4", languageId: "plaintext");
            await Task.Delay(2000);

            Assert.Empty(Diagnostics);
        }

        [SkippableFact]
        public async Task CanReceiveDiagnosticsFromFileChanged()
        {
            Skip.If(
                TestsFixture.RunningInConstainedLanguageMode && TestsFixture.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string filePath = NewTestFile("$a = 4");
            await WaitForDiagnostics();
            Diagnostics.Clear();

            PsesLanguageClient.SendNotification("textDocument/didChange", new DidChangeTextDocumentParams
            {
                // Include several content changes to test against duplicate Diagnostics showing up.
                ContentChanges = new Container<TextDocumentContentChangeEvent>(new []
                {
                    new TextDocumentContentChangeEvent
                    {
                        Text = "$a = 5"
                    },
                    new TextDocumentContentChangeEvent
                    {
                        Text = "$a = 6"
                    },
                    new TextDocumentContentChangeEvent
                    {
                        Text = "$a = 7"
                    }
                }),
                TextDocument = new VersionedTextDocumentIdentifier
                {
                    Version = 4,
                    Uri = new Uri(filePath)
                }
            });

            await WaitForDiagnostics();
            if (Diagnostics.Count > 1)
            {
                StringBuilder errorBuilder = new StringBuilder().AppendLine("Multiple diagnostics found when there should be only 1:");
                foreach (Diagnostic diag in Diagnostics)
                {
                    errorBuilder.AppendLine(diag.Message);
                }

                Assert.True(Diagnostics.Count == 1, errorBuilder.ToString());
            }

            Diagnostic diagnostic = Assert.Single(Diagnostics);
            Assert.Equal("PSUseDeclaredVarsMoreThanAssignments", diagnostic.Code);
        }

        [SkippableFact]
        public async Task CanReceiveDiagnosticsFromConfigurationChange()
        {
            Skip.If(
                TestsFixture.RunningInConstainedLanguageMode && TestsFixture.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            NewTestFile("gci | % { $_ }");
            await WaitForDiagnostics();

            // NewTestFile doesn't clear diagnostic notifications so we need to do that for this test.
            Diagnostics.Clear();

            try
            {
                PsesLanguageClient.SendNotification("workspace/didChangeConfiguration",
                new DidChangeConfigurationParams
                {
                    Settings = JToken.Parse(@"
{
    ""powershell"": {
        ""scriptAnalysis"": {
            ""enable"": false
        }
    }
}
")
                });

                Assert.Empty(Diagnostics);
            }
            finally
            {
                PsesLanguageClient.SendNotification("workspace/didChangeConfiguration",
                new DidChangeConfigurationParams
                {
                    Settings = JToken.Parse(@"
{
    ""powershell"": {
        ""scriptAnalysis"": {
            ""enable"": true
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
                await PsesLanguageClient
                    .SendRequest<FoldingRangeRequestParam>(
                        "textDocument/foldingRange",
                        new FoldingRangeRequestParam
                        {
                            TextDocument = new TextDocumentIdentifier
                            {
                                Uri = new Uri(scriptPath)
                            }
                        })
                    .Returning<Container<FoldingRange>>(CancellationToken.None);

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

        [SkippableFact]
        public async Task CanSendFormattingRequest()
        {
            Skip.If(
                TestsFixture.RunningInConstainedLanguageMode && TestsFixture.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string scriptPath = NewTestFile(@"
gci | % {
Get-Process
}

");

            TextEditContainer textEdits = await PsesLanguageClient
                .SendRequest<DocumentFormattingParams>(
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
                    })
                .Returning<TextEditContainer>(CancellationToken.None);

            TextEdit textEdit = Assert.Single(textEdits);

            // If we have a tab, formatting ran.
            Assert.Contains("\t", textEdit.NewText);
        }

        [SkippableFact]
        public async Task CanSendRangeFormattingRequest()
        {
            Skip.If(
                TestsFixture.RunningInConstainedLanguageMode && TestsFixture.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string scriptPath = NewTestFile(@"
gci | % {
Get-Process
}

");

            TextEditContainer textEdits = await PsesLanguageClient
                .SendRequest<DocumentRangeFormattingParams>(
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
                    })
                .Returning<TextEditContainer>(CancellationToken.None);

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
                await PsesLanguageClient
                    .SendRequest<DocumentSymbolParams>(
                        "textDocument/documentSymbol",
                        new DocumentSymbolParams
                        {
                            TextDocument = new TextDocumentIdentifier
                            {
                                Uri = new Uri(scriptPath)
                            }
                        })
                    .Returning<SymbolInformationOrDocumentSymbolContainer>(CancellationToken.None);

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

            LocationContainer locations = await PsesLanguageClient
                .SendRequest<ReferenceParams>(
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
                    })
                .Returning<LocationContainer>(CancellationToken.None);

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
                await PsesLanguageClient
                    .SendRequest<DocumentHighlightParams>(
                        "textDocument/documentHighlight",
                        new DocumentHighlightParams
                        {
                            TextDocument = new TextDocumentIdentifier
                            {
                                Uri = new Uri(scriptPath)
                            },
                            Position = new Position
                            {
                                Line = 3,
                                Character = 1
                            }
                        })
                    .Returning<DocumentHighlightContainer>(CancellationToken.None);

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
                    await PsesLanguageClient
                        .SendRequest<GetPSHostProcesssesParams>(
                            "powerShell/getPSHostProcesses",
                            new GetPSHostProcesssesParams { })
                        .Returning<PSHostProcessResponse[]>(CancellationToken.None);
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
                    await PsesLanguageClient
                        .SendRequest<GetRunspaceParams>(
                            "powerShell/getRunspace",
                            new GetRunspaceParams
                            {
                                ProcessId = $"{process.Id}"
                            })
                        .Returning<RunspaceResponse[]>(CancellationToken.None);
            }
            finally
            {
                process.Kill();
                process.Dispose();
            }

            Assert.NotEmpty(runspaceResponses);
        }

        [Fact]
        public async Task CanSendPesterLegacyCodeLensRequest()
        {
            // Make sure LegacyCodeLens is enabled because we'll need it in this test.
            PsesLanguageClient.Workspace.DidChangeConfiguration(
                new DidChangeConfigurationParams
                {
                    Settings = JObject.Parse(@"
{
    ""powershell"": {
        ""pester"": {
            ""useLegacyCodeLens"": true
        }
    }
}
")
                });

            string filePath = NewTestFile(@"
Describe 'DescribeName' {
    Context 'ContextName' {
        It 'ItName' {
            1 | Should - Be 1
        }
    }
}
", isPester: true);

            CodeLensContainer codeLenses = await PsesLanguageClient
                .SendRequest<CodeLensParams>(
                    "textDocument/codeLens",
                    new CodeLensParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(filePath)
                        }
                    })
                .Returning<CodeLensContainer>(CancellationToken.None);

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
        public async Task CanSendPesterCodeLensRequest()
        {
            // Make sure Pester legacy CodeLens is disabled because we'll need it in this test.
            PsesLanguageClient.Workspace.DidChangeConfiguration(
                new DidChangeConfigurationParams
                {
                    Settings = JObject.Parse(@"
{
    ""powershell"": {
        ""pester"": {
            ""useLegacyCodeLens"": false
        }
    }
}
")
                });

            string filePath = NewTestFile(@"
Describe 'DescribeName' {
    Context 'ContextName' {
        It 'ItName' {
            1 | Should - Be 1
        }
    }
}
", isPester: true);

            CodeLensContainer codeLenses = await PsesLanguageClient
                .SendRequest<CodeLensParams>(
                    "textDocument/codeLens",
                    new CodeLensParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(filePath)
                        }
                    })
                .Returning<CodeLensContainer>(CancellationToken.None);

            Assert.Collection(codeLenses,
                codeLens =>
                {
                    Range range = codeLens.Range;

                    Assert.Equal(1, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(7, range.End.Line);
                    Assert.Equal(1, range.End.Character);

                    Assert.Equal("Run tests", codeLens.Command.Title);
                },
                codeLens =>
                {
                    Range range = codeLens.Range;

                    Assert.Equal(1, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(7, range.End.Line);
                    Assert.Equal(1, range.End.Character);

                    Assert.Equal("Debug tests", codeLens.Command.Title);
                },
                codeLens =>
                {
                    Range range = codeLens.Range;

                    Assert.Equal(2, range.Start.Line);
                    Assert.Equal(4, range.Start.Character);
                    Assert.Equal(6, range.End.Line);
                    Assert.Equal(5, range.End.Character);

                    Assert.Equal("Run tests", codeLens.Command.Title);
                },
                codeLens =>
                {
                    Range range = codeLens.Range;

                    Assert.Equal(2, range.Start.Line);
                    Assert.Equal(4, range.Start.Character);
                    Assert.Equal(6, range.End.Line);
                    Assert.Equal(5, range.End.Character);

                    Assert.Equal("Debug tests", codeLens.Command.Title);
                },
                codeLens =>
                {
                    Range range = codeLens.Range;

                    Assert.Equal(3, range.Start.Line);
                    Assert.Equal(8, range.Start.Character);
                    Assert.Equal(5, range.End.Line);
                    Assert.Equal(9, range.End.Character);

                    Assert.Equal("Run test", codeLens.Command.Title);
                },
                codeLens =>
                {
                    Range range = codeLens.Range;

                    Assert.Equal(3, range.Start.Line);
                    Assert.Equal(8, range.Start.Character);
                    Assert.Equal(5, range.End.Line);
                    Assert.Equal(9, range.End.Character);

                    Assert.Equal("Debug test", codeLens.Command.Title);
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

            CodeLensContainer codeLenses = await PsesLanguageClient
                .SendRequest<CodeLensParams>(
                    "textDocument/codeLens",
                    new CodeLensParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(filePath)
                        }
                    })
                .Returning<CodeLensContainer>(CancellationToken.None);

            CodeLens codeLens = Assert.Single(codeLenses);

            Range range = codeLens.Range;
            Assert.Equal(1, range.Start.Line);
            Assert.Equal(0, range.Start.Character);
            Assert.Equal(3, range.End.Line);
            Assert.Equal(1, range.End.Character);

            CodeLens codeLensResolveResult = await PsesLanguageClient
                .SendRequest<CodeLens>("codeLens/resolve", codeLens)
                .Returning<CodeLens>(CancellationToken.None);

            Assert.Equal("1 reference", codeLensResolveResult.Command.Title);
        }

        [SkippableFact]
        public async Task CanSendCodeActionRequest()
        {
            Skip.If(
                TestsFixture.RunningInConstainedLanguageMode && TestsFixture.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string filePath = NewTestFile("gci");
            await WaitForDiagnostics();

            CommandOrCodeActionContainer commandOrCodeActions =
                await PsesLanguageClient
                    .SendRequest<CodeActionParams>(
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
                        })
                    .Returning<CommandOrCodeActionContainer>(CancellationToken.None);

            Assert.Collection(commandOrCodeActions,
                command =>
                {
                    Assert.Equal(
                        "Replace gci with Get-ChildItem",
                        command.CodeAction.Title);
                    Assert.Equal(
                        CodeActionKind.QuickFix,
                        command.CodeAction.Kind);
                    Assert.Single(command.CodeAction.Edit.DocumentChanges);
                },
                command =>
                {
                    Assert.Equal(
                        "PowerShell.ShowCodeActionDocumentation",
                        command.CodeAction.Command.Name);
                });
        }

        [Fact]
        public async Task CanSendCompletionAndCompletionResolveRequest()
        {
            string filePath = NewTestFile("Write-H");

            CompletionList completionItems = await PsesLanguageClient.TextDocument.RequestCompletion(
                new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = DocumentUri.FromFileSystemPath(filePath)
                    },
                    Position = new Position(line: 0, character: 7)
                });

            CompletionItem completionItem = Assert.Single(completionItems,
                completionItem1 => completionItem1.Label == "Write-Host");

            CompletionItem updatedCompletionItem = await PsesLanguageClient
                .SendRequest<CompletionItem>("completionItem/resolve", completionItem)
                .Returning<CompletionItem>(CancellationToken.None);

            Assert.Contains("Writes customized output to a host", updatedCompletionItem.Documentation.String);
        }

        [Fact]
        public async Task CanSendCompletionResolveWithModulePrefixRequest()
        {
            await PsesLanguageClient
                .SendRequest<EvaluateRequestArguments>(
                    "evaluate",
                    new EvaluateRequestArguments
                    {
                        Expression = "Import-Module Microsoft.PowerShell.Archive -Prefix Slow"
                    })
                .ReturningVoid(CancellationToken.None);

            string filePath = NewTestFile("Expand-SlowArch");

            CompletionList completionItems = await PsesLanguageClient.TextDocument.RequestCompletion(
                new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = DocumentUri.FromFileSystemPath(filePath)
                    },
                    Position = new Position(line: 0, character: 15)
                });

            CompletionItem completionItem = Assert.Single(completionItems,
                completionItem1 => completionItem1.Label == "Expand-SlowArchive");

            CompletionItem updatedCompletionItem = await PsesLanguageClient
                .SendRequest<CompletionItem>("completionItem/resolve", completionItem)
                .Returning<CompletionItem>(CancellationToken.None);

            Assert.Contains("Extracts files from a specified archive", updatedCompletionItem.Documentation.String);
        }

        [Fact]
        public async Task CanSendHoverRequest()
        {
            string filePath = NewTestFile("Write-Host");

            Hover hover = await PsesLanguageClient.TextDocument.RequestHover(
                new HoverParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = DocumentUri.FromFileSystemPath(filePath)
                    },
                    Position = new Position(line: 0, character: 1)
                });

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

            SignatureHelp signatureHelp = await PsesLanguageClient
                .SendRequest<SignatureHelpParams>(
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
                    })
                .Returning<SignatureHelp>(CancellationToken.None);

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
                await PsesLanguageClient
                    .SendRequest<DefinitionParams>(
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
                        })
                    .Returning<LocationOrLocationLinks>(CancellationToken.None);

            LocationOrLocationLink locationOrLocationLink =
                    Assert.Single(locationOrLocationLinks);

            Assert.Equal(1, locationOrLocationLink.Location.Range.Start.Line);
            Assert.Equal(9, locationOrLocationLink.Location.Range.Start.Character);
            Assert.Equal(1, locationOrLocationLink.Location.Range.End.Line);
            Assert.Equal(33, locationOrLocationLink.Location.Range.End.Character);
        }

        [SkippableFact]
        public async Task CanSendGetProjectTemplatesRequest()
        {
            Skip.If(TestsFixture.RunningInConstainedLanguageMode, "Plaster doesn't work in ConstrainedLanguage mode.");

            GetProjectTemplatesResponse getProjectTemplatesResponse =
                await PsesLanguageClient
                    .SendRequest<GetProjectTemplatesRequest>(
                        "powerShell/getProjectTemplates",
                        new GetProjectTemplatesRequest
                        {
                            IncludeInstalledModules = true
                        })
                    .Returning<GetProjectTemplatesResponse>(CancellationToken.None);

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

        [SkippableFact]
        public async Task CanSendGetCommentHelpRequest()
        {
            Skip.If(
                TestsFixture.RunningInConstainedLanguageMode && TestsFixture.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string scriptPath = NewTestFile(@"
function CanSendGetCommentHelpRequest {
    param(
        $myParam,
        $myOtherParam,
        $yetAnotherParam
    )

    # Include other problematic code to make sure this still works
    gci
}
");

            CommentHelpRequestResult commentHelpRequestResult =
                await PsesLanguageClient
                    .SendRequest<CommentHelpRequestParams>(
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
                        })
                    .Returning<CommentHelpRequestResult>(CancellationToken.None);

            Assert.NotEmpty(commentHelpRequestResult.Content);
            Assert.Contains("myParam", commentHelpRequestResult.Content[7]);
        }

        [Fact]
        public async Task CanSendEvaluateRequest()
        {
            EvaluateResponseBody evaluateResponseBody =
                await PsesLanguageClient
                    .SendRequest<EvaluateRequestArguments>(
                        "evaluate",
                        new EvaluateRequestArguments
                        {
                            Expression = "Get-ChildItem"
                        })
                    .Returning<EvaluateResponseBody>(CancellationToken.None);

            // These always gets returned so this test really just makes sure we get _any_ response.
            Assert.Equal("", evaluateResponseBody.Result);
            Assert.Equal(0, evaluateResponseBody.VariablesReference);
        }

        [Fact]
        public async Task CanSendGetCommandRequest()
        {
            List<object> pSCommandMessages =
                await PsesLanguageClient
                    .SendRequest<GetCommandParams>("powerShell/getCommand", new GetCommandParams())
                    .Returning<List<object>>(CancellationToken.None);

            Assert.NotEmpty(pSCommandMessages);
            // There should be at least 20 commands or so.
            Assert.True(pSCommandMessages.Count > 20);
        }

        [SkippableFact]
        public async Task CanSendExpandAliasRequest()
        {
            Skip.If(
                TestsFixture.RunningInConstainedLanguageMode,
                "This feature currently doesn't support ConstrainedLanguage Mode.");

            ExpandAliasResult expandAliasResult =
                await PsesLanguageClient
                    .SendRequest<ExpandAliasParams>(
                        "powerShell/expandAlias",
                        new ExpandAliasParams
                        {
                            Text = "gci"
                        })
                    .Returning<ExpandAliasResult>(CancellationToken.None);

            Assert.Equal("Get-ChildItem", expandAliasResult.Text);
        }
    }
}
