//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Test.Shared.Utility;
using System;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Linq;
using Xunit;
using Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint;
using Microsoft.PowerShell.EditorServices.Test.Shared.Definition;
using Microsoft.PowerShell.EditorServices.Test.Shared.References;
using Microsoft.PowerShell.EditorServices.Test.Shared.Occurrences;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class LanguageServiceTests : IDisposable
    {
        private ResourceFileLoader fileLoader;
        private Runspace languageServiceRunspace;
        private LanguageService languageService;

        public LanguageServiceTests()
        {
            // Load script files from the shared assembly
            this.fileLoader =
                new ResourceFileLoader(
                    typeof(CompleteCommandInFile).Assembly);

            this.languageServiceRunspace = RunspaceFactory.CreateRunspace();
            this.languageServiceRunspace.ApartmentState = ApartmentState.STA;
            this.languageServiceRunspace.ThreadOptions = PSThreadOptions.ReuseThread;
            this.languageServiceRunspace.Open();

            this.languageService = new LanguageService(this.languageServiceRunspace);
        }

        public void Dispose()
        {
            this.languageServiceRunspace.Dispose();
        }

        [Fact]
        public void LanguageServiceCompletesCommandInFile()
        {
            CompletionResults completionResults =
                this.GetCompletionResults(
                    CompleteCommandInFile.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteCommandInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact(Skip = "This test does not run correctly on AppVeyor, need to investigate.")]
        public void LanguageServiceCompletesCommandFromModule()
        {
            CompletionResults completionResults =
                this.GetCompletionResults(
                    CompleteCommandFromModule.SourceDetails);

            Assert.NotEqual(0, completionResults.Completions.Length);
            Assert.Equal(
                CompleteCommandFromModule.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public void LanguageServiceCompletesVariableInFile()
        {
            CompletionResults completionResults =
                this.GetCompletionResults(
                    CompleteVariableInFile.SourceDetails);

            Assert.Equal(1, completionResults.Completions.Length);
            Assert.Equal(
                CompleteVariableInFile.ExpectedCompletion,
                completionResults.Completions[0]);
        }

        [Fact]
        public void LanguageServiceFindsParameterHintsOnCommand()
        {
            ParameterSetSignatures paramSignatures =
                this.GetParamSetSignatures(
                    FindsParameterSetsOnCommand.SourceDetails);

            Assert.NotNull(paramSignatures);
            Assert.Equal("Get-Process", paramSignatures.CommandName);
            Assert.Equal(6, paramSignatures.Signatures.Count());
        }

        [Fact]
        public void LanguageServiceFindsCommandForParamHintsWithSpaces()
        {
            ParameterSetSignatures paramSignatures =
                this.GetParamSetSignatures(
                    FindsParameterSetsOnCommandWithSpaces.SourceDetails);

            Assert.NotNull(paramSignatures);
            Assert.Equal("Write-Host", paramSignatures.CommandName);
            Assert.Equal(1, paramSignatures.Signatures.Count());
        }

        [Fact]
        public void LanguageServiceFindsFunctionDefinition()
        {
            GetDefinitionResult definitionResult =
                this.GetDefinition(
                    FindsFunctionDefinition.SourceDetails);

            SymbolReference definition = definitionResult.FoundDefinition;
            Assert.Equal(1, definition.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definition.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definition.SymbolName);
        }

        [Fact]
        public void LanguageServiceFindsVariableDefinition()
        {
            GetDefinitionResult definitionResult =
                this.GetDefinition(
                    FindsVariableDefinition.SourceDetails);

            SymbolReference definition = definitionResult.FoundDefinition;
            Assert.Equal(6, definition.ScriptRegion.StartLineNumber);
            Assert.Equal(1, definition.ScriptRegion.StartColumnNumber);
            Assert.Equal("$things", definition.SymbolName);
        }

        [Fact]
        public void LanguageServiceFindsFunctionReferences()
        {
            FindReferencesResult referencesResult =
                this.GetReferences(
                    FindsReferencesOnFunction.SourceDetails);

            Assert.Equal(3, referencesResult.FoundReferences.Count());
            Assert.Equal(1, referencesResult.FoundReferences.First().ScriptRegion.StartLineNumber);
            Assert.Equal(10, referencesResult.FoundReferences.First().ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void LanguageServiceFindsVariableReferences()
        {
            FindReferencesResult referencesResult =
                this.GetReferences(
                    FindsReferencesOnVariable.SourceDetails);

            Assert.Equal(3, referencesResult.FoundReferences.Count());
            Assert.Equal(10, referencesResult.FoundReferences.Last().ScriptRegion.StartLineNumber);
            Assert.Equal(13, referencesResult.FoundReferences.Last().ScriptRegion.StartColumnNumber);
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

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion){
            return this.fileLoader.LoadFile(scriptRegion.File);
        }


        private CompletionResults GetCompletionResults(ScriptRegion scriptRegion)
        {
            // Run the completions request
            return
                this.languageService.GetCompletionsInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private ParameterSetSignatures GetParamSetSignatures(ScriptRegion scriptRegion)
        {
            return
                this.languageService.FindParameterSetsInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private GetDefinitionResult GetDefinition(ScriptRegion scriptRegion)
        {
            return
                this.languageService.GetDefinitionInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
        }

        private FindReferencesResult GetReferences(ScriptRegion scriptRegion)
        {
            return
                this.languageService.FindReferencesInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);
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
