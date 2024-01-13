// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services.Configuration;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.Template;
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
    [Trait("Category", "LSP")]
    public class LanguageServerProtocolMessageTests : IClassFixture<LSPTestsFixture>, IDisposable
    {
        // Borrowed from `VersionUtils` which can't be used here due to an initialization problem.
        private static bool IsLinux { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        private static readonly string s_binDir =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private readonly ILanguageClient PsesLanguageClient;
        private readonly List<LogMessageParams> Messages;
        private readonly List<Diagnostic> Diagnostics;
        private readonly string PwshExe;

        public LanguageServerProtocolMessageTests(ITestOutputHelper output, LSPTestsFixture data)
        {
            data.Output = output;
            PsesLanguageClient = data.PsesLanguageClient;
            Messages = data.Messages;
            Messages.Clear();
            Diagnostics = data.Diagnostics;
            Diagnostics.Clear();
            PwshExe = PsesStdioProcess.PwshExe;
        }

        public void Dispose()
        {
            Diagnostics.Clear();
            GC.SuppressFinalize(this);
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

        private async Task WaitForDiagnosticsAsync()
        {
            // Wait for PSSA to finish.
            for (int i = 0; Diagnostics.Count == 0; i++)
            {
                if (i >= 30)
                {
                    throw new InvalidDataException("No diagnostics showed up after 20s.");
                }

                await Task.Delay(1000);
            }
        }

        [Fact]
        public async Task CanSendPowerShellGetVersionRequestAsync()
        {
            PowerShellVersion details
                = await PsesLanguageClient
                    .SendRequest("powerShell/getVersion", new GetVersionParams())
                    .Returning<PowerShellVersion>(CancellationToken.None);

            if (PwshExe == "powershell")
            {
                Assert.Equal("Desktop", details.Edition);
                Assert.StartsWith("5", details.Version);
            }
            else
            {
                Assert.Equal("Core", details.Edition);
                Assert.StartsWith("7", details.Version);
            }
        }

        [Fact]
        public async Task CanSendWorkspaceSymbolRequestAsync()
        {
            NewTestFile(@"
function CanSendWorkspaceSymbolRequest {
    Write-Host 'hello'
}
");

            Container<WorkspaceSymbol> symbols = await PsesLanguageClient
                .SendRequest(
                    "workspace/symbol",
                    new WorkspaceSymbolParams
                    {
                        Query = "CanSendWorkspaceSymbolRequest"
                    })
                .Returning<Container<WorkspaceSymbol>>(CancellationToken.None);

            WorkspaceSymbol symbol = Assert.Single(symbols);
            Assert.Equal("function CanSendWorkspaceSymbolRequest ()", symbol.Name);
        }

        [SkippableFact]
        public async Task CanReceiveDiagnosticsFromFileOpenAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode && PsesStdioProcess.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            NewTestFile("$a = 4");
            await WaitForDiagnosticsAsync();

            Diagnostic diagnostic = Assert.Single(Diagnostics);
            Assert.Equal("PSUseDeclaredVarsMoreThanAssignments", diagnostic.Code);
        }

        [Fact]
        public async Task WontReceiveDiagnosticsFromFileOpenThatIsNotPowerShellAsync()
        {
            NewTestFile("$a = 4", languageId: "plaintext");
            await Task.Delay(2000);

            Assert.Empty(Diagnostics);
        }

        [SkippableFact]
        public async Task CanReceiveDiagnosticsFromFileChangedAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode && PsesStdioProcess.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string filePath = NewTestFile("$a = 4");
            await WaitForDiagnosticsAsync();
            Diagnostics.Clear();

            PsesLanguageClient.SendNotification("textDocument/didChange", new DidChangeTextDocumentParams
            {
                // Include several content changes to test against duplicate Diagnostics showing up.
                ContentChanges = new Container<TextDocumentContentChangeEvent>(new[]
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
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Version = 4,
                    Uri = new Uri(filePath)
                }
            });

            await WaitForDiagnosticsAsync();
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
        public async Task CanReceiveDiagnosticsFromConfigurationChangeAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode && PsesStdioProcess.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            PsesLanguageClient.SendNotification("workspace/didChangeConfiguration",
                new DidChangeConfigurationParams
                {
                    Settings = JToken.FromObject(new LanguageServerSettingsWrapper
                    {
                        Files = new EditorFileSettings(),
                        Search = new EditorSearchSettings(),
                        Powershell = new LanguageServerSettings
                        {
                            ScriptAnalysis = new ScriptAnalysisSettings
                            {
                                Enable = false
                            }
                        }
                    })
                });

            string filePath = NewTestFile("$a = 4");

            // Wait a bit to make sure no diagnostics came through
            await Task.Delay(2000);
            Assert.Empty(Diagnostics);

            // Restore default configuration
            PsesLanguageClient.SendNotification("workspace/didChangeConfiguration",
                new DidChangeConfigurationParams
                {
                    Settings = JToken.FromObject(new LanguageServerSettingsWrapper
                    {
                        Files = new EditorFileSettings(),
                        Search = new EditorSearchSettings(),
                        Powershell = new LanguageServerSettings()
                    })
                });

            // That notification does not trigger re-analyzing open files. For that we have to send
            // a textDocument/didChange notification.
            PsesLanguageClient.SendNotification("textDocument/didChange", new DidChangeTextDocumentParams
            {
                ContentChanges = new Container<TextDocumentContentChangeEvent>(),
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    Version = 4,
                    Uri = new Uri(filePath)
                }
            });

            await WaitForDiagnosticsAsync();

            Diagnostic diagnostic = Assert.Single(Diagnostics);
            Assert.Equal("PSUseDeclaredVarsMoreThanAssignments", diagnostic.Code);
        }

        [Fact]
        public async Task CanSendFoldingRangeRequestAsync()
        {
            string scriptPath = NewTestFile(@"gci | % {
$_

@""
    $_
""@
}");

            Container<FoldingRange> foldingRanges =
                await PsesLanguageClient
                    .SendRequest(
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
        public async Task CanSendFormattingRequestAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode && PsesStdioProcess.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string scriptPath = NewTestFile(@"
gci | % {
Get-Process
}

");

            TextEditContainer textEdits = await PsesLanguageClient
                .SendRequest(
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
        public async Task CanSendRangeFormattingRequestAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode && PsesStdioProcess.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string scriptPath = NewTestFile(@"
gci | % {
Get-Process
}

");

            TextEditContainer textEdits = await PsesLanguageClient
                .SendRequest(
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
        public async Task CanSendDocumentSymbolRequestAsync()
        {
            string scriptPath = NewTestFile(@"
function CanSendDocumentSymbolRequest {

}

CanSendDocumentSymbolRequest
");

            SymbolInformationOrDocumentSymbolContainer symbolInformationOrDocumentSymbols =
                await PsesLanguageClient
                    .SendRequest(
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
                symInfoOrDocSym =>
                {
                    Assert.True(symInfoOrDocSym.IsDocumentSymbol);
                    Assert.NotNull(symInfoOrDocSym.DocumentSymbol);
                    DocumentSymbol symbol = symInfoOrDocSym.DocumentSymbol;

                    Assert.Equal("function CanSendDocumentSymbolRequest ()", symbol.Name);
                    Assert.Equal(SymbolKind.Function, symbol.Kind);

                    Assert.Equal(1, symbol.Range.Start.Line);
                    Assert.Equal(0, symbol.Range.Start.Character);
                    Assert.Equal(3, symbol.Range.End.Line);
                    Assert.Equal(1, symbol.Range.End.Character);

                    Assert.Equal(1, symbol.SelectionRange.Start.Line);
                    Assert.Equal(9, symbol.SelectionRange.Start.Character);
                    Assert.Equal(1, symbol.SelectionRange.End.Line);
                    Assert.Equal(37, symbol.SelectionRange.End.Character);
                });
        }

        [Fact]
        public async Task CanSendReferencesRequestAsync()
        {
            string scriptPath = NewTestFile(@"
function CanSendReferencesRequest {

}

CanSendReferencesRequest
");

            LocationContainer locations = await PsesLanguageClient
                .SendRequest(
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
                location =>
                {
                    Range range = location.Range;
                    Assert.Equal(5, range.Start.Line);
                    Assert.Equal(0, range.Start.Character);
                    Assert.Equal(5, range.End.Line);
                    Assert.Equal(24, range.End.Character);
                });
        }

        [Fact]
        public async Task CanSendDocumentHighlightRequestAsync()
        {
            string scriptPath = NewTestFile(@"
Write-Host 'Hello!'

Write-Host 'Goodbye'
");

            DocumentHighlightContainer documentHighlights =
                await PsesLanguageClient
                    .SendRequest(
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

            Assert.Collection(documentHighlights.OrderBy(i => i.Range.Start.Line),
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
        public async Task CanSendPowerShellGetPSHostProcessesRequestAsync()
        {
            Process process = new();
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
                        .SendRequest(
                            "powerShell/getPSHostProcesses",
                            new GetPSHostProcessesParams())
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
        public async Task CanSendPowerShellGetRunspaceRequestAsync()
        {
            Process process = new();
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
                        .SendRequest(
                            "powerShell/getRunspace",
                            new GetRunspaceParams
                            {
                                ProcessId = process.Id
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
        public async Task CanSendPesterLegacyCodeLensRequestAsync()
        {
            // Make sure LegacyCodeLens is enabled because we'll need it in this test.
            PsesLanguageClient.Workspace.DidChangeConfiguration(
                new DidChangeConfigurationParams
                {
                    Settings = JObject.Parse(@"
{
    ""powershell"": {
        ""pester"": {
            ""useLegacyCodeLens"": true,
            ""codeLens"": true
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
                .SendRequest(
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
        public async Task CanSendPesterCodeLensRequestAsync()
        {
            // Make sure Pester legacy CodeLens is disabled because we'll need it in this test.
            PsesLanguageClient.Workspace.DidChangeConfiguration(
                new DidChangeConfigurationParams
                {
                    Settings = JObject.Parse(@"
{
    ""powershell"": {
        ""pester"": {
            ""useLegacyCodeLens"": false,
            ""codeLens"": true
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
                .SendRequest(
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
        public async Task NoMessageIfPesterCodeLensDisabled()
        {
            // Make sure Pester legacy CodeLens is disabled because we'll need it in this test.
            PsesLanguageClient.Workspace.DidChangeConfiguration(
                new DidChangeConfigurationParams
                {
                    Settings = JObject.Parse(@"
{
    ""powershell"": {
        ""pester"": {
            ""codeLens"": false
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
                .SendRequest(
                    "textDocument/codeLens",
                    new CodeLensParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(filePath)
                        }
                    })
                .Returning<CodeLensContainer>(CancellationToken.None);

            Assert.Empty(codeLenses);
        }

        [Fact]
        public async Task CanSendFunctionReferencesCodeLensRequestAsync()
        {
            string filePath = NewTestFile(@"
function CanSendReferencesCodeLensRequest {

}

CanSendReferencesCodeLensRequest
");

            CodeLensContainer codeLenses = await PsesLanguageClient
                .SendRequest(
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
            Assert.Equal(9, range.Start.Character);
            Assert.Equal(1, range.End.Line);
            Assert.Equal(41, range.End.Character);

            CodeLens codeLensResolveResult = await PsesLanguageClient
                .SendRequest("codeLens/resolve", codeLens)
                .Returning<CodeLens>(CancellationToken.None);

            Assert.Equal("1 reference", codeLensResolveResult.Command.Title);
        }

        [Fact]
        public async Task CanSendClassReferencesCodeLensRequestAsync()
        {
            string filePath = NewTestFile(@"
param(
    [MyBaseClass]$enumValue
)

class MyBaseClass {

}

class ChildClass : MyBaseClass, System.IDisposable {

}

$o = [MyBaseClass]::new()
$o -is [MyBaseClass]
");

            CodeLensContainer codeLenses = await PsesLanguageClient
                .SendRequest(
                    "textDocument/codeLens",
                    new CodeLensParams
                    {
                        TextDocument = new TextDocumentIdentifier
                        {
                            Uri = new Uri(filePath)
                        }
                    })
                .Returning<CodeLensContainer>(CancellationToken.None);

            Assert.Collection(codeLenses.OrderBy(i => i.Range.Start.Line),
                codeLens =>
                {
                    Range range = codeLens.Range;
                    Assert.Equal(5, range.Start.Line);
                    Assert.Equal(6, range.Start.Character);
                    Assert.Equal(5, range.End.Line);
                    Assert.Equal(17, range.End.Character);
                },
                codeLens =>
                {
                    Range range = codeLens.Range;
                    Assert.Equal(9, range.Start.Line);
                    Assert.Equal(6, range.Start.Character);
                    Assert.Equal(9, range.End.Line);
                    Assert.Equal(16, range.End.Character);
                }
            );

            CodeLens baseClassCodeLens = codeLenses.OrderBy(i => i.Range.Start.Line).First();
            CodeLens codeLensResolveResult = await PsesLanguageClient
                .SendRequest("codeLens/resolve", baseClassCodeLens)
                .Returning<CodeLens>(CancellationToken.None);

            Assert.Equal("4 references", codeLensResolveResult.Command.Title);
        }

        [Fact]
        public async Task CanSendEnumReferencesCodeLensRequestAsync()
        {
            string filePath = NewTestFile(@"
param(
    [MyEnum]$enumValue
)

enum MyEnum {
    First = 1
    Second
    Third
}

[MyEnum]::First
'First' -is [MyEnum]
");

            CodeLensContainer codeLenses = await PsesLanguageClient
                .SendRequest(
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
            Assert.Equal(5, range.Start.Line);
            Assert.Equal(5, range.Start.Character);
            Assert.Equal(5, range.End.Line);
            Assert.Equal(11, range.End.Character);

            CodeLens codeLensResolveResult = await PsesLanguageClient
                .SendRequest("codeLens/resolve", codeLens)
                .Returning<CodeLens>(CancellationToken.None);

            Assert.Equal("3 references", codeLensResolveResult.Command.Title);
        }

        [SkippableFact]
        public async Task CanSendCodeActionRequestAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode && PsesStdioProcess.IsWindowsPowerShell,
                "Windows PowerShell doesn't trust PSScriptAnalyzer by default so it won't load.");

            string filePath = NewTestFile("gci");
            await WaitForDiagnosticsAsync();

            CommandOrCodeActionContainer commandOrCodeActions =
                await PsesLanguageClient
                    .SendRequest(
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

        [SkippableFact]
        public async Task CanSendCompletionAndCompletionResolveRequestAsync()
        {
            Skip.If(IsLinux, "This depends on the help system, which is flaky on Linux.");
            Skip.If(PsesStdioProcess.IsWindowsPowerShell, "This help system isn't updated in CI.");
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
                completionItem1 => completionItem1.FilterText == "Write-Host");

            CompletionItem updatedCompletionItem = await PsesLanguageClient
                .SendRequest("completionItem/resolve", completionItem)
                .Returning<CompletionItem>(CancellationToken.None);

            Assert.Contains("Writes customized output to a host", updatedCompletionItem.Documentation.String);
        }

        // Regression test for https://github.com/PowerShell/PowerShellEditorServices/issues/1926
        [SkippableFact]
        public async Task CanRequestCompletionsAndHandleExceptions()
        {
            PowerShellVersion details
                = await PsesLanguageClient
                    .SendRequest("powerShell/getVersion", new GetVersionParams())
                    .Returning<PowerShellVersion>(CancellationToken.None);

            Skip.IfNot(details.Version.StartsWith("7.2") || details.Version.StartsWith("7.3"),
                "This is a bug in PowerShell 7.2 and 7.3, fixed in 7.4");

            string filePath = NewTestFile(@"
@() | ForEach-Object {
    if ($false) {
      return
    }

    @{key=$}
  }");

            Messages.Clear(); //  On some systems there's a warning message about configuration items too.
            CompletionList completionItems = await PsesLanguageClient.TextDocument.RequestCompletion(
                new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier
                    {
                        Uri = DocumentUri.FromFileSystemPath(filePath)
                    },
                    Position = new Position(line: 6, character: 11)
                });

            Assert.Empty(completionItems);
            LogMessageParams message = Assert.Single(Messages);
            Assert.Contains("Exception occurred while running handling completion request", message.Message);
        }

        [SkippableFact(Skip = "Completion for Expand-SlowArchive is flaky.")]
        public async Task CanSendCompletionResolveWithModulePrefixRequestAsync()
        {
            await PsesLanguageClient
                .SendRequest(
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
                .SendRequest("completionItem/resolve", completionItem)
                .Returning<CompletionItem>(CancellationToken.None);

            Assert.Contains("Extracts files from a specified archive", updatedCompletionItem.Documentation.String);
        }

        [SkippableFact]
        public async Task CanSendHoverRequestAsync()
        {
            Skip.If(IsLinux, "This depends on the help system, which is flaky on Linux.");
            Skip.If(PsesStdioProcess.IsWindowsPowerShell, "This help system isn't updated in CI.");
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
                str1 => Assert.Equal("Write-Host", str1.Value),
                str2 =>
                {
                    Assert.Equal("markdown", str2.Language);
                    Assert.Equal("Writes customized output to a host.", str2.Value);
                });
        }

        [Fact]
        public async Task CanSendSignatureHelpRequestAsync()
        {
            string filePath = NewTestFile("Get-Date -");

            SignatureHelp signatureHelp = await PsesLanguageClient
                .SendRequest(
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
                            Character = 10
                        }
                    })
                .Returning<SignatureHelp>(CancellationToken.None);

            Assert.Contains("Get-Date", signatureHelp.Signatures.First().Label);
        }

        [Fact]
        public async Task CanSendDefinitionRequestAsync()
        {
            string scriptPath = NewTestFile(@"
function CanSendDefinitionRequest {

}

CanSendDefinitionRequest
");

            LocationOrLocationLinks locationOrLocationLinks =
                await PsesLanguageClient
                    .SendRequest(
                        "textDocument/definition",
                        new DefinitionParams
                        {
                            TextDocument = new TextDocumentIdentifier { Uri = new Uri(scriptPath) },
                            Position = new Position { Line = 5, Character = 2 }
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
        public async Task CanSendGetProjectTemplatesRequestAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode,
                "Plaster doesn't work in Constrained Language Mode.");

            GetProjectTemplatesResponse getProjectTemplatesResponse =
                await PsesLanguageClient
                    .SendRequest(
                        "powerShell/getProjectTemplates",
                        new GetProjectTemplatesRequest
                        {
                            IncludeInstalledModules = true
                        })
                    .Returning<GetProjectTemplatesResponse>(CancellationToken.None);

            Assert.Contains(getProjectTemplatesResponse.Templates, t => t.Title is "AddPSScriptAnalyzerSettings");
            Assert.Contains(getProjectTemplatesResponse.Templates, t => t.Title is "New PowerShell Manifest Module");
        }

        [SkippableFact]
        public async Task CanSendGetCommentHelpRequestAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode && PsesStdioProcess.IsWindowsPowerShell,
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
                    .SendRequest(
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
        public async Task CanSendEvaluateRequestAsync()
        {
            EvaluateResponseBody evaluateResponseBody =
                await PsesLanguageClient
                    .SendRequest(
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
        public async Task CanSendGetCommandRequestAsync()
        {
            List<object> pSCommandMessages =
                await PsesLanguageClient
                    .SendRequest("powerShell/getCommand", new GetCommandParams())
                    .Returning<List<object>>(CancellationToken.None);

            Assert.NotEmpty(pSCommandMessages);
            // There should be at least 20 commands or so.
            Assert.True(pSCommandMessages.Count > 20);
        }

        [SkippableFact]
        public async Task CanSendExpandAliasRequestAsync()
        {
            Skip.If(PsesStdioProcess.RunningInConstrainedLanguageMode,
                "The expand alias request doesn't work in Constrained Language Mode.");

            ExpandAliasResult expandAliasResult =
                await PsesLanguageClient
                    .SendRequest(
                        "powerShell/expandAlias",
                        new ExpandAliasParams
                        {
                            Text = "gci"
                        })
                    .Returning<ExpandAliasResult>(CancellationToken.None);

            Assert.Equal("Get-ChildItem", expandAliasResult.Text);
        }

        [Fact]
        public async Task CanSendSemanticTokenRequestAsync()
        {
            const string scriptContent = "function";
            string scriptPath = NewTestFile(scriptContent);

            SemanticTokens result =
                await PsesLanguageClient
                    .SendRequest(
                        "textDocument/semanticTokens/full",
                        new SemanticTokensParams
                        {
                            TextDocument = new TextDocumentIdentifier
                            {
                                Uri = new Uri(scriptPath)
                            }
                        })
                    .Returning<SemanticTokens>(CancellationToken.None);

            // More information about how this data is generated can be found at
            // https://github.com/microsoft/vscode-extension-samples/blob/5ae1f7787122812dcc84e37427ca90af5ee09f14/semantic-tokens-sample/vscode.proposed.d.ts#L71
            int[] expectedArr = new int[5]
                {
                    // line, index, token length, token type, token modifiers
                    0, 0, scriptContent.Length, 1, 0 //function token: line 0, index 0, length of script, type 1 = keyword, no modifiers
                };

            Assert.Equal(expectedArr, result.Data.ToArray());
        }
    }
}
