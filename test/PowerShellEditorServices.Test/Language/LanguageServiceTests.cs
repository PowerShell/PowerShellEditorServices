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
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class LanguageServiceTests : IDisposable
    {
        private Workspace workspace;
        private LanguageService languageService;
        private PowerShellContext powerShellContext;
        private const string baseSharedScriptPath = @"..\..\..\..\PowerShellEditorServices.Test.Shared\";

        public LanguageServiceTests()
        {
            var logger = Logging.NullLogger;
            this.powerShellContext = PowerShellContextFactory.Create(logger);
            this.workspace = new Workspace(this.powerShellContext.LocalPowerShellVersion.Version, logger);
            this.languageService = new LanguageService(this.powerShellContext, logger);
        }

        public void Dispose()
        {
            this.powerShellContext.Dispose();
        }

        [Fact]
        public async Task LanguageServiceCompletesCommandInFile()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteCommandInFile.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteCommandInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public async Task LanguageServiceCompletesCommandFromModule()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteCommandFromModule.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteCommandFromModule.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public async Task LanguageServiceCompletesVariableInFile()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteVariableInFile.SourceDetails);

            Assert.Equal(1, completionResults.Completions.Length);
            Assert.Equal(
                CompleteVariableInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public async Task LanguageServiceCompletesAttributeValue()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteAttributeValue.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteAttributeValue.ExpectedRange,
                completionResults.ReplacedRange);
        }

        [Fact]
        public async Task LanguageServiceCompletesFilePath()
        {
            CompletionResults completionResults =
                await this.GetCompletionResults(
                    CompleteFilePath.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteFilePath.ExpectedRange,
                completionResults.ReplacedRange);
        }

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

        [Fact]
        public async Task LanguageServiceFindsCommandForParamHintsWithSpaces()
        {
            ParameterSetSignatures paramSignatures =
                await this.GetParamSetSignatures(
                    FindsParameterSetsOnCommandWithSpaces.SourceDetails);

            Assert.NotNull(paramSignatures);
            Assert.Equal("Write-Host", paramSignatures.CommandName);
            Assert.Equal(1, paramSignatures.Signatures.Count());
        }

        [Fact]
        public async Task LanguageServiceFindsFunctionDefinition()
        {
            GetDefinitionResult definitionResult =
                await this.GetDefinition(
                    FindsFunctionDefinition.SourceDetails);

            SymbolReference definition = definitionResult.FoundDefinition;
            Assert.Equal(1, definition.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definition.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definition.SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsFunctionDefinitionInDotSourceReference()
        {
            GetDefinitionResult definitionResult =
                await this.GetDefinition(
                    FindsFunctionDefinitionInDotSourceReference.SourceDetails);

            SymbolReference definition = definitionResult.FoundDefinition;
            Assert.True(
                definitionResult.FoundDefinition.FilePath.EndsWith(
                    FindsFunctionDefinition.SourceDetails.File),
                "Unexpected reference file: " + definitionResult.FoundDefinition.FilePath);
            Assert.Equal(1, definition.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definition.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definition.SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsFunctionDefinitionInWorkspace()
        {
            var definitionResult =
                await this.GetDefinition(
                    FindsFunctionDefinitionInWorkspace.SourceDetails,
                    new Workspace(this.powerShellContext.LocalPowerShellVersion.Version, Logging.NullLogger)
                    {
                        WorkspacePath = Path.Combine(baseSharedScriptPath, @"References")
                    });
            var definition = definitionResult.FoundDefinition;
            Assert.EndsWith("ReferenceFileE.ps1", definition.FilePath);
            Assert.Equal("My-FunctionInFileE", definition.SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsVariableDefinition()
        {
            GetDefinitionResult definitionResult =
                await this.GetDefinition(
                    FindsVariableDefinition.SourceDetails);

            SymbolReference definition = definitionResult.FoundDefinition;
            Assert.Equal(6, definition.ScriptRegion.StartLineNumber);
            Assert.Equal(1, definition.ScriptRegion.StartColumnNumber);
            Assert.Equal("$things", definition.SymbolName);
        }

        [Fact]
        public void LanguageServiceFindsOccurrencesOnFunction()
        {
            FindOccurrencesResult occurrencesResult =
                this.GetOccurrences(
                    FindsOccurrencesOnFunction.SourceDetails);

            Assert.Equal(3, occurrencesResult.FoundOccurrences.Count());
            Assert.Equal(10, occurrencesResult.FoundOccurrences.Last().ScriptRegion.StartLineNumber);
            Assert.Equal(1, occurrencesResult.FoundOccurrences.Last().ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void LanguageServiceFindsOccurrencesOnParameter()
        {
            FindOccurrencesResult occurrencesResult =
                this.GetOccurrences(
                    FindOccurrencesOnParameter.SourceDetails);

            Assert.Equal("$myInput", occurrencesResult.FoundOccurrences.Last().SymbolName);
            Assert.Equal(2, occurrencesResult.FoundOccurrences.Count());
            Assert.Equal(3, occurrencesResult.FoundOccurrences.Last().ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task LanguageServiceFindsReferencesOnCommandWithAlias()
        {
            FindReferencesResult refsResult =
                await this.GetReferences(
                    FindsReferencesOnBuiltInCommandWithAlias.SourceDetails);

            Assert.Equal(6, refsResult.FoundReferences.Count());
            Assert.Equal("Get-ChildItem", refsResult.FoundReferences.Last().SymbolName);
            Assert.Equal("ls", refsResult.FoundReferences.ToArray()[1].SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsReferencesOnAlias()
        {
            FindReferencesResult refsResult =
                await this.GetReferences(
                    FindsReferencesOnBuiltInCommandWithAlias.SourceDetails);

            Assert.Equal(6, refsResult.FoundReferences.Count());
            Assert.Equal("Get-ChildItem", refsResult.FoundReferences.Last().SymbolName);
            Assert.Equal("gci", refsResult.FoundReferences.ToArray()[2].SymbolName);
            Assert.Equal("LS", refsResult.FoundReferences.ToArray()[4].SymbolName);
        }

        [Fact]
        public async Task LanguageServiceFindsReferencesOnFileWithReferencesFileB()
        {
            FindReferencesResult refsResult =
                await this.GetReferences(
                    FindsReferencesOnFunctionMultiFileDotSourceFileB.SourceDetails);

            Assert.Equal(4, refsResult.FoundReferences.Count());
        }

        [Fact]
        public async Task LanguageServiceFindsReferencesOnFileWithReferencesFileC()
        {
            FindReferencesResult refsResult =
                await this.GetReferences(
                    FindsReferencesOnFunctionMultiFileDotSourceFileC.SourceDetails);
            Assert.Equal(4, refsResult.FoundReferences.Count());
        }

        [Fact]
        public async Task LanguageServiceFindsDetailsForBuiltInCommand()
        {
            SymbolDetails symbolDetails =
                await this.languageService.FindSymbolDetailsAtLocation(
                    this.GetScriptFile(FindsDetailsForBuiltInCommand.SourceDetails),
                    FindsDetailsForBuiltInCommand.SourceDetails.StartLineNumber,
                    FindsDetailsForBuiltInCommand.SourceDetails.StartColumnNumber);

            Assert.NotNull(symbolDetails.Documentation);
            Assert.NotEqual("", symbolDetails.Documentation);
        }

        [Fact]
        public void LanguageServiceFindsSymbolsInFile()
        {
            FindOccurrencesResult symbolsResult =
                this.FindSymbolsInFile(
                    FindSymbolsInMultiSymbolFile.SourceDetails);

            Assert.Equal(4, symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Function).Count());
            Assert.Equal(3, symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Variable).Count());
            Assert.Equal(1, symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Workflow).Count());

            SymbolReference firstFunctionSymbol = symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Function).First();
            Assert.Equal("AFunction", firstFunctionSymbol.SymbolName);
            Assert.Equal(7, firstFunctionSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstFunctionSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference lastVariableSymbol = symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Variable).Last();
            Assert.Equal("$Script:ScriptVar2", lastVariableSymbol.SymbolName);
            Assert.Equal(3, lastVariableSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, lastVariableSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstWorkflowSymbol = symbolsResult.FoundOccurrences.Where(r => r.SymbolType == SymbolType.Workflow).First();
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

        [Fact]
        public void LanguageServiceFindsSymbolsInPesterFile()
        {
            var symbolsResult = this.FindSymbolsInFile(FindSymbolsInPesterFile.SourceDetails);
            Assert.Equal(5, symbolsResult.FoundOccurrences.Count());
        }

        [Fact]
        public void LangServerFindsSymbolsInPSDFile()
        {
            var symbolsResult = this.FindSymbolsInFile(FindSymbolsInPSDFile.SourceDetails);
            Assert.Equal(3, symbolsResult.FoundOccurrences.Count());
        }

        [Fact]
        public void LanguageServiceFindsSymbolsInNoSymbolsFile()
        {
            FindOccurrencesResult symbolsResult =
                this.FindSymbolsInFile(
                    FindSymbolsInNoSymbolsFile.SourceDetails);

            Assert.Equal(0, symbolsResult.FoundOccurrences.Count());
        }

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion)
        {
            string resolvedPath =
                Path.Combine(
                    baseSharedScriptPath,
                    scriptRegion.File);

            return
                this.workspace.GetFile(
                    resolvedPath);
        }

        private async Task<CompletionResults> GetCompletionResults(ScriptRegion scriptRegion)
        {
            // Run the completions request
            return
                await this.languageService.GetCompletionsInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private async Task<ParameterSetSignatures> GetParamSetSignatures(ScriptRegion scriptRegion)
        {
            return
                await this.languageService.FindParameterSetsInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private async Task<GetDefinitionResult> GetDefinition(ScriptRegion scriptRegion, Workspace workspace)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference =
                this.languageService.FindSymbolAtLocation(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return
                await this.languageService.GetDefinitionOfSymbol(
                    scriptFile,
                    symbolReference,
                    workspace);
        }

        private async Task<GetDefinitionResult> GetDefinition(ScriptRegion scriptRegion)
        {
            return await GetDefinition(scriptRegion, this.workspace);
        }

        private async Task<FindReferencesResult> GetReferences(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference =
                this.languageService.FindSymbolAtLocation(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return
                await this.languageService.FindReferencesOfSymbol(
                    symbolReference,
                    this.workspace.ExpandScriptReferences(scriptFile),
                    this.workspace);
        }

        private FindOccurrencesResult GetOccurrences(ScriptRegion scriptRegion)
        {
            return
                this.languageService.FindOccurrencesInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private FindOccurrencesResult FindSymbolsInFile(ScriptRegion scriptRegion)
        {
            return
                this.languageService.FindSymbolsInFile(
                    GetScriptFile(scriptRegion));
        }
    }
}
