//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Test.Shared.Definition;
using Microsoft.PowerShell.EditorServices.Test.Shared.Occurrences;
using Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint;
using Microsoft.PowerShell.EditorServices.Test.Shared.References;
using Microsoft.PowerShell.EditorServices.Test.Shared.SymbolDetails;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class LanguageServiceTests : IDisposable
    {
        private Workspace workspace;
        private LanguageService languageService;
        private PowerShellSession powerShellSession;

        public LanguageServiceTests()
        {
            this.workspace = new Workspace();

            this.powerShellSession = new PowerShellSession();
            this.languageService = new LanguageService(this.powerShellSession);
        }

        public void Dispose()
        {
            this.powerShellSession.Dispose();
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

        [Fact(Skip = "This test does not run correctly on AppVeyor, need to investigate.")]
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
        
        private ScriptFile GetScriptFile(ScriptRegion scriptRegion)
        {
            const string baseSharedScriptPath = 
                @"..\..\..\PowerShellEditorServices.Test.Shared\";

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

        private async Task<GetDefinitionResult> GetDefinition(ScriptRegion scriptRegion)
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
                    this.workspace);
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
                    this.workspace.ExpandScriptReferences(scriptFile));
        }

        private FindOccurrencesResult GetOccurrences(ScriptRegion scriptRegion)
        { 
            return
                this.languageService.FindOccurrencesInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }
    }
}
