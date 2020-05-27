//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Test.Shared.Definition;
using Microsoft.PowerShell.EditorServices.Test.Shared.Occurrences;
using Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint;
using Microsoft.PowerShell.EditorServices.Test.Shared.References;
using Microsoft.PowerShell.EditorServices.Test.Shared.SymbolDetails;
using Microsoft.PowerShell.EditorServices.Test.Shared.Symbols;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using System.Collections.Generic;
using Microsoft.PowerShell.EditorServices.Handlers;
using System.Runtime.InteropServices;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class LanguageServiceTests : IDisposable
    {
        private readonly WorkspaceService workspace;
        private readonly SymbolsService symbolsService;
        private readonly PsesCompletionHandler completionHandler;
        private readonly PowerShellContextService powerShellContext;
        private static readonly string s_baseSharedScriptPath =
            Path.Combine(
                    Path.GetDirectoryName(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        // On non-Windows platforms, CodeBase has file:// in it.
                        // On Windows, Location points to a temp directory.
                        ? typeof(LanguageServiceTests).Assembly.CodeBase
                        : typeof(LanguageServiceTests).Assembly.Location),
                    "..","..","..","..",
                    "PowerShellEditorServices.Test.Shared");

        public LanguageServiceTests()
        {
            var logger = NullLogger.Instance;
            powerShellContext = PowerShellContextFactory.Create(logger);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
            symbolsService = new SymbolsService(NullLoggerFactory.Instance, powerShellContext, workspace, new ConfigurationService());
            completionHandler = new PsesCompletionHandler(NullLoggerFactory.Instance, powerShellContext, workspace);
        }

        public void Dispose()
        {
            this.powerShellContext.Close();
        }

        [Trait("Category", "Completions")]
        [Fact]
        public async Task LanguageServiceCompletesCommandInFile()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteCommandInFile.SourceDetails);

            Assert.NotEmpty(completionResults.Completions);
            Assert.Equal(
                CompleteCommandInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Trait("Category", "Completions")]
        [Fact]
        public async Task LanguageServiceCompletesCommandFromModule()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteCommandFromModule.SourceDetails);

            Assert.NotEmpty(completionResults.Completions);
            Assert.Equal(
                CompleteCommandFromModule.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Trait("Category", "Completions")]
        [Fact]
        public async Task LanguageServiceCompletesVariableInFile()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteVariableInFile.SourceDetails);

            Assert.Single(completionResults.Completions);
            Assert.Equal(
                CompleteVariableInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Trait("Category", "Completions")]
        [Fact]
        public async Task LanguageServiceCompletesAttributeValue()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteAttributeValue.SourceDetails);

            Assert.NotEmpty(completionResults.Completions);
            Assert.Equal(
                CompleteAttributeValue.ExpectedRange,
                completionResults.ReplacedRange);
        }

        [Trait("Category", "Completions")]
        [Fact]
        public async Task LanguageServiceCompletesFilePath()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteFilePath.SourceDetails);

            Assert.NotEmpty(completionResults.Completions);
            // TODO: Since this is a path completion, this test will need to be
            //       platform specific. Probably something like:
            //         - Windows: C:\Program
            //         - macOS:   /User
            //         - Linux:   /hom
            //Assert.Equal(
            //    CompleteFilePath.ExpectedRange,
            //    completionResults.ReplacedRange);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public async Task LanguageServiceFindsParameterHintsOnCommand()
        {
            ParameterSetSignatures paramSignatures =
                await this.GetParamSetSignatures(
                    FindsParameterSetsOnCommand.SourceDetails);

            Assert.NotNull(paramSignatures);
            Assert.Equal("Get-Process", paramSignatures.CommandName);
            Assert.Equal(6, paramSignatures.Signatures.Count());
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public async Task LanguageServiceFindsCommandForParamHintsWithSpaces()
        {
            ParameterSetSignatures paramSignatures =
                await this.GetParamSetSignatures(
                    FindsParameterSetsOnCommandWithSpaces.SourceDetails);

            Assert.NotNull(paramSignatures);
            Assert.Equal("Write-Host", paramSignatures.CommandName);
            Assert.Single(paramSignatures.Signatures);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public async Task LanguageServiceFindsFunctionDefinition()
        {
            SymbolReference definitionResult =
                await this.GetDefinition(
                    FindsFunctionDefinition.SourceDetails);

            Assert.Equal(1, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definitionResult.SymbolName);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public async Task LanguageServiceFindsFunctionDefinitionInDotSourceReference()
        {
            SymbolReference definitionResult =
                await this.GetDefinition(
                    FindsFunctionDefinitionInDotSourceReference.SourceDetails);

            Assert.True(
                definitionResult.FilePath.EndsWith(
                    FindsFunctionDefinition.SourceDetails.File),
                "Unexpected reference file: " + definitionResult.FilePath);
            Assert.Equal(1, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definitionResult.SymbolName);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public async Task LanguageServiceFindsDotSourcedFile()
        {
            SymbolReference definitionResult =
                await this.GetDefinition(
                    FindsDotSourcedFile.SourceDetails);

            Assert.True(
                definitionResult.FilePath.EndsWith(
                    Path.Combine("References", "ReferenceFileE.ps1")),
                "Unexpected reference file: " + definitionResult.FilePath);
            Assert.Equal(1, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(1, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("./ReferenceFileE.ps1", definitionResult.SymbolName);
        }

        [Trait("Category", "Symbols")]
        [Fact(Skip = "TODO Fix this test. A possible bug in PSES product code.")]
        public async Task LanguageServiceFindsFunctionDefinitionInWorkspace()
        {
            SymbolReference definitionResult =
                await this.GetDefinition(
                    FindsFunctionDefinitionInWorkspace.SourceDetails);
            Assert.EndsWith("ReferenceFileE.ps1", definitionResult.FilePath);
            Assert.Equal("My-FunctionInFileE", definitionResult.SymbolName);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public async Task LanguageServiceFindsVariableDefinition()
        {
            SymbolReference definitionResult =
                await this.GetDefinition(
                    FindsVariableDefinition.SourceDetails);

            Assert.Equal(6, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(1, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("$things", definitionResult.SymbolName);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public void LanguageServiceFindsOccurrencesOnFunction()
        {
            IReadOnlyList<SymbolReference> occurrencesResult =
                this.GetOccurrences(
                    FindsOccurrencesOnFunction.SourceDetails);

            Assert.Equal(3, occurrencesResult.Count());
            Assert.Equal(10, occurrencesResult.Last().ScriptRegion.StartLineNumber);
            Assert.Equal(1, occurrencesResult.Last().ScriptRegion.StartColumnNumber);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public void LanguageServiceFindsOccurrencesOnParameter()
        {
            IReadOnlyList<SymbolReference> occurrencesResult =
                this.GetOccurrences(
                    FindOccurrencesOnParameter.SourceDetails);

            Assert.Equal("$myInput", occurrencesResult.Last().SymbolName);
            Assert.Equal(2, occurrencesResult.Count());
            Assert.Equal(3, occurrencesResult.Last().ScriptRegion.StartLineNumber);
        }

        [Trait("Category", "Symbols")]
        [Fact(Skip = "TODO Fix this test. A possible bug in PSES product code.")]
        public void LanguageServiceFindsReferencesOnCommandWithAlias()
        {
            List<SymbolReference> refsResult =
                this.GetReferences(
                    FindsReferencesOnBuiltInCommandWithAlias.SourceDetails);

            SymbolReference[] foundRefs = refsResult.ToArray();
            Assert.Equal(4, foundRefs.Length);
            Assert.Equal("gci", foundRefs[1].SymbolName);
            Assert.Equal("Get-ChildItem", foundRefs[foundRefs.Length - 1].SymbolName);
        }

        [Trait("Category", "Symbols")]
        [Fact(Skip = "TODO Fix this test. A possible bug in PSES product code.")]
        public void LanguageServiceFindsReferencesOnAlias()
        {
            List<SymbolReference> refsResult =
                this.GetReferences(
                    FindsReferencesOnBuiltInCommandWithAlias.SourceDetails);

            Assert.Equal(4, refsResult.Count());
            Assert.Equal("dir", refsResult.ToArray()[2].SymbolName);
            Assert.Equal("Get-ChildItem", refsResult.Last().SymbolName);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public void LanguageServiceFindsReferencesOnFileWithReferencesFileB()
        {
            List<SymbolReference> refsResult =
                this.GetReferences(
                    FindsReferencesOnFunctionMultiFileDotSourceFileB.SourceDetails);

            Assert.Equal(4, refsResult.Count());
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public void LanguageServiceFindsReferencesOnFileWithReferencesFileC()
        {
            List<SymbolReference> refsResult =
                this.GetReferences(
                    FindsReferencesOnFunctionMultiFileDotSourceFileC.SourceDetails);
            Assert.Equal(4, refsResult.Count());
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public async Task LanguageServiceFindsDetailsForBuiltInCommand()
        {
            SymbolDetails symbolDetails =
                await this.symbolsService.FindSymbolDetailsAtLocationAsync(
                    this.GetScriptFile(FindsDetailsForBuiltInCommand.SourceDetails),
                    FindsDetailsForBuiltInCommand.SourceDetails.StartLineNumber,
                    FindsDetailsForBuiltInCommand.SourceDetails.StartColumnNumber);

            Assert.NotNull(symbolDetails.Documentation);
            Assert.NotEqual("", symbolDetails.Documentation);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public void LanguageServiceFindsSymbolsInFile()
        {
            List<SymbolReference> symbolsResult =
                this.FindSymbolsInFile(
                    FindSymbolsInMultiSymbolFile.SourceDetails);

            Assert.Equal(4, symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Function).Count());
            Assert.Equal(3, symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Variable).Count());
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Workflow));

            SymbolReference firstFunctionSymbol = symbolsResult.Where(r => r.SymbolType == SymbolType.Function).First();
            Assert.Equal("AFunction", firstFunctionSymbol.SymbolName);
            Assert.Equal(7, firstFunctionSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstFunctionSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference lastVariableSymbol = symbolsResult.Where(r => r.SymbolType == SymbolType.Variable).Last();
            Assert.Equal("$Script:ScriptVar2", lastVariableSymbol.SymbolName);
            Assert.Equal(3, lastVariableSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, lastVariableSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstWorkflowSymbol = symbolsResult.Where(r => r.SymbolType == SymbolType.Workflow).First();
            Assert.Equal("AWorkflow", firstWorkflowSymbol.SymbolName);
            Assert.Equal(23, firstWorkflowSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstWorkflowSymbol.ScriptRegion.StartColumnNumber);

            // TODO: Bring this back when we can use AstVisitor2 again (#276)
            //Assert.Equal(1, symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Configuration).Count());
            //SymbolReference firstConfigurationSymbol = symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Configuration).First();
            //Assert.Equal("AConfiguration", firstConfigurationSymbol.SymbolName);
            //Assert.Equal(25, firstConfigurationSymbol.ScriptRegion.StartLineNumber);
            //Assert.Equal(1, firstConfigurationSymbol.ScriptRegion.StartColumnNumber);
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public void LanguageServiceFindsSymbolsInPesterFile()
        {
            List<SymbolReference> symbolsResult = this.FindSymbolsInFile(FindSymbolsInPesterFile.SourceDetails);
            Assert.Equal(5, symbolsResult.Count());
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public void LangServerFindsSymbolsInPSDFile()
        {
            List<SymbolReference> symbolsResult = this.FindSymbolsInFile(FindSymbolsInPSDFile.SourceDetails);
            Assert.Equal(3, symbolsResult.Count());
        }

        [Trait("Category", "Symbols")]
        [Fact]
        public void LanguageServiceFindsSymbolsInNoSymbolsFile()
        {
            List<SymbolReference> symbolsResult =
                this.FindSymbolsInFile(
                    FindSymbolsInNoSymbolsFile.SourceDetails);

            Assert.Empty(symbolsResult);
        }

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion)
        {
            string resolvedPath =
                Path.Combine(
                    s_baseSharedScriptPath,
                    scriptRegion.File);

            return
                this.workspace.GetFile(
                    resolvedPath);
        }

        private async Task<CompletionResults> GetCompletionResults(ScriptRegion scriptRegion)
        {
            // Run the completions request
            return
                await this.completionHandler.GetCompletionsInFileAsync(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private async Task<ParameterSetSignatures> GetParamSetSignatures(ScriptRegion scriptRegion)
        {
            return
                await this.symbolsService.FindParameterSetsInFileAsync(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber,
                    powerShellContext);
        }

        private async Task<SymbolReference> GetDefinition(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference =
                this.symbolsService.FindSymbolAtLocation(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return
                await this.symbolsService.GetDefinitionOfSymbolAsync(
                    scriptFile,
                    symbolReference);
        }

        private List<SymbolReference> GetReferences(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference =
                this.symbolsService.FindSymbolAtLocation(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return
                this.symbolsService.FindReferencesOfSymbol(
                    symbolReference,
                    this.workspace.ExpandScriptReferences(scriptFile),
                    this.workspace);
        }

        private IReadOnlyList<SymbolReference> GetOccurrences(ScriptRegion scriptRegion)
        {
            return
                this.symbolsService.FindOccurrencesInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private List<SymbolReference> FindSymbolsInFile(ScriptRegion scriptRegion)
        {
            return
                this.symbolsService.FindSymbolsInFile(
                    GetScriptFile(scriptRegion));
        }
    }
}
