//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Language;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Test.Shared.Completion;
using Microsoft.PowerShell.EditorServices.Test.Shared.Definition;
using Microsoft.PowerShell.EditorServices.Test.Shared.Occurrences;
using Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint;
using Microsoft.PowerShell.EditorServices.Test.Shared.References;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    public class LanguageServiceTests : IDisposable
    {
        private Workspace workspace;
        private Runspace languageServiceRunspace;
        private LanguageService languageService;

        public LanguageServiceTests()
        {
            this.workspace = new Workspace();

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
        public void LanguageServiceFindsFunctionDefinitionInDotSourceReference()
        {
            GetDefinitionResult definitionResult =
                this.GetDefinition(
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
        public void LanguageServiceFindsReferencesOnCommandWithAlias()
        {
            FindReferencesResult refsResult =
                this.GetReferences(
                    FindsReferencesOnBuiltInCommandWithAlias.SourceDetails);

            Assert.Equal(6, refsResult.FoundReferences.Count());
            Assert.Equal("Get-ChildItem", refsResult.FoundReferences.Last().SymbolName);
            Assert.Equal("ls", refsResult.FoundReferences.ToArray()[1].SymbolName);
        }

        [Fact]
        public void LanguageServiceFindsReferencesOnAlias()
        {
            FindReferencesResult refsResult =
                this.GetReferences(
                    FindsReferencesOnBuiltInCommandWithAlias.SourceDetails);

            Assert.Equal(6, refsResult.FoundReferences.Count());
            Assert.Equal("Get-ChildItem", refsResult.FoundReferences.Last().SymbolName);
            Assert.Equal("gci", refsResult.FoundReferences.ToArray()[2].SymbolName);
            Assert.Equal("LS", refsResult.FoundReferences.ToArray()[4].SymbolName);
        }

        [Fact]
        public void LanguageServiceFindsReferencesOnFileWithReferencesFileB()
        {
            FindReferencesResult refsResult =
                this.GetReferences(
                    FindsReferencesOnFunctionMultiFileDotSourceFileB.SourceDetails);

            Assert.Equal(4, refsResult.FoundReferences.Count());
        }
        
        [Fact]
        public void LanguageServiceFindsReferencesOnFileWithReferencesFileC()
        {
            FindReferencesResult refsResult =
                this.GetReferences(
                    FindsReferencesOnFunctionMultiFileDotSourceFileC.SourceDetails);
            Assert.Equal(4, refsResult.FoundReferences.Count());
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
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference =
                this.languageService.FindSymbolAtLocation(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return
                this.languageService.GetDefinitionOfSymbol(
                    scriptFile,
                    symbolReference,
                    this.workspace);
        }

        private FindReferencesResult GetReferences(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference =
                this.languageService.FindSymbolAtLocation(
                    scriptFile,
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return
                this.languageService.FindReferencesOfSymbol(
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
