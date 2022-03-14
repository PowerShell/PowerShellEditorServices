// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Test.Shared.Definition;
using Microsoft.PowerShell.EditorServices.Test.Shared.Occurrences;
using Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint;
using Microsoft.PowerShell.EditorServices.Test.Shared.References;
using Microsoft.PowerShell.EditorServices.Test.Shared.SymbolDetails;
using Microsoft.PowerShell.EditorServices.Test.Shared.Symbols;
using Xunit;

namespace Microsoft.PowerShell.EditorServices.Test.Language
{
    [Trait("Category", "Symbols")]
    public class SymbolsServiceTests : IDisposable
    {
        private readonly PsesInternalHost psesHost;
        private readonly WorkspaceService workspace;
        private readonly SymbolsService symbolsService;

        public SymbolsServiceTests()
        {
            psesHost = PsesHostFactory.Create(NullLoggerFactory.Instance);
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
            symbolsService = new SymbolsService(
                NullLoggerFactory.Instance,
                psesHost,
                psesHost,
                workspace,
                new ConfigurationService());
        }

        public void Dispose()
        {
            psesHost.StopAsync().GetAwaiter().GetResult();
            CommandHelpers.s_cmdletToAliasCache.Clear();
            CommandHelpers.s_aliasToCmdletCache.Clear();
            GC.SuppressFinalize(this);
        }

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion) => workspace.GetFile(TestUtilities.GetSharedPath(scriptRegion.File));

        private Task<ParameterSetSignatures> GetParamSetSignatures(ScriptRegion scriptRegion)
        {
            return symbolsService.FindParameterSetsInFileAsync(
                GetScriptFile(scriptRegion),
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber);
        }

        private Task<SymbolReference> GetDefinition(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference = SymbolsService.FindSymbolAtLocation(
                scriptFile,
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return symbolsService.GetDefinitionOfSymbolAsync(scriptFile, symbolReference);
        }

        private Task<List<SymbolReference>> GetReferences(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbolReference = SymbolsService.FindSymbolAtLocation(
                scriptFile,
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber);

            Assert.NotNull(symbolReference);

            return symbolsService.FindReferencesOfSymbol(
                symbolReference,
                workspace.ExpandScriptReferences(scriptFile),
                workspace);
        }

        private IReadOnlyList<SymbolReference> GetOccurrences(ScriptRegion scriptRegion)
        {
            return SymbolsService.FindOccurrencesInFile(
                GetScriptFile(scriptRegion),
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber);
        }

        private List<SymbolReference> FindSymbolsInFile(ScriptRegion scriptRegion) => symbolsService.FindSymbolsInFile(GetScriptFile(scriptRegion));

        [Fact]
        public async Task FindsParameterHintsOnCommand()
        {
            ParameterSetSignatures paramSignatures = await GetParamSetSignatures(FindsParameterSetsOnCommandData.SourceDetails).ConfigureAwait(true);
            Assert.NotNull(paramSignatures);
            Assert.Equal("Get-Process", paramSignatures.CommandName);
            Assert.Equal(6, paramSignatures.Signatures.Length);
        }

        [Fact]
        public async Task FindsCommandForParamHintsWithSpaces()
        {
            ParameterSetSignatures paramSignatures = await GetParamSetSignatures(FindsParameterSetsOnCommandWithSpacesData.SourceDetails).ConfigureAwait(true);
            Assert.NotNull(paramSignatures);
            Assert.Equal("Write-Host", paramSignatures.CommandName);
            Assert.Single(paramSignatures.Signatures);
        }

        [Fact]
        public async Task FindsFunctionDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsFunctionDefinitionData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(1, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsFunctionDefinitionForAlias()
        {
            // TODO: Eventually we should get the alises through the AST instead of relying on them
            // being defined in the runspace.
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("Set-Alias -Name My-Alias -Value My-Function"),
                CancellationToken.None).ConfigureAwait(true);

            SymbolReference definitionResult = await GetDefinition(FindsFunctionDefinitionOfAliasData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(1, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnFunction()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnFunctionData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(3, referencesResult.Count);
            Assert.Equal(1, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(10, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public async Task FindsReferencesOnFunctionIncludingAliases()
        {
            // TODO: Same as in FindsFunctionDefinitionForAlias.
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("Set-Alias -Name My-Alias -Value My-Function"),
                CancellationToken.None).ConfigureAwait(true);

            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnFunctionData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(4, referencesResult.Count);
            Assert.Equal(1, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(10, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public async Task FindsFunctionDefinitionInDotSourceReference()
        {
            SymbolReference definitionResult = await GetDefinition(FindsFunctionDefinitionInDotSourceReferenceData.SourceDetails).ConfigureAwait(true);
            Assert.True(
                definitionResult.FilePath.EndsWith(FindsFunctionDefinitionData.SourceDetails.File),
                "Unexpected reference file: " + definitionResult.FilePath);
            Assert.Equal(1, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(10, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("My-Function", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsDotSourcedFile()
        {
            SymbolReference definitionResult = await GetDefinition(FindsDotSourcedFileData.SourceDetails).ConfigureAwait(true);
            Assert.True(
                definitionResult.FilePath.EndsWith(Path.Combine("References", "ReferenceFileE.ps1")),
                "Unexpected reference file: " + definitionResult.FilePath);
            Assert.Equal(1, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(1, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("./ReferenceFileE.ps1", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsFunctionDefinitionInWorkspace()
        {
            workspace.WorkspacePath = TestUtilities.GetSharedPath("References");
            SymbolReference definitionResult = await GetDefinition(FindsFunctionDefinitionInWorkspaceData.SourceDetails).ConfigureAwait(true);
            Assert.EndsWith("ReferenceFileE.ps1", definitionResult.FilePath);
            Assert.Equal("My-FunctionInFileE", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsVariableDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsVariableDefinitionData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(6, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(1, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("$things", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnVariable()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnVariableData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(3, referencesResult.Count);
            Assert.Equal(10, referencesResult[referencesResult.Count - 1].ScriptRegion.StartLineNumber);
            Assert.Equal(13, referencesResult[referencesResult.Count - 1].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnFunction()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnFunctionData.SourceDetails);
            Assert.Equal(3, occurrencesResult.Count);
            Assert.Equal(10, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
            Assert.Equal(1, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnParameter()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindOccurrencesOnParameterData.SourceDetails);
            Assert.Equal(2, occurrencesResult.Count);
            Assert.Equal("$myInput", occurrencesResult[occurrencesResult.Count - 1].SymbolName);
            Assert.Equal(3, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsReferencesOnCommandWithAlias()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnBuiltInCommandWithAliasData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(4, referencesResult.Count);
            Assert.Equal("gci", referencesResult[1].SymbolName);
            Assert.Equal("dir", referencesResult[2].SymbolName);
            Assert.Equal("Get-ChildItem", referencesResult[referencesResult.Count - 1].SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnFileWithReferencesFileB()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnFunctionMultiFileDotSourceFileB.SourceDetails).ConfigureAwait(true);
            Assert.Equal(4, referencesResult.Count);
        }

        [Fact]
        public async Task FindsReferencesOnFileWithReferencesFileC()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnFunctionMultiFileDotSourceFileC.SourceDetails).ConfigureAwait(true);
            Assert.Equal(4, referencesResult.Count);
        }

        [Fact]
        public async Task FindsDetailsForBuiltInCommand()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForBuiltInCommandData.SourceDetails),
                FindsDetailsForBuiltInCommandData.SourceDetails.StartLineNumber,
                FindsDetailsForBuiltInCommandData.SourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.NotNull(symbolDetails.Documentation);
            Assert.NotEqual("", symbolDetails.Documentation);
        }

        [Fact]
        public void FindsSymbolsInFile()
        {
            List<SymbolReference> symbolsResult =
                FindSymbolsInFile(
                    FindSymbolsInMultiSymbolFile.SourceDetails);

            Assert.Equal(4, symbolsResult.Count(symbolReference => symbolReference.SymbolType == SymbolType.Function));
            Assert.Equal(3, symbolsResult.Count(symbolReference => symbolReference.SymbolType == SymbolType.Variable));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Workflow));

            SymbolReference firstFunctionSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Function);
            Assert.Equal("AFunction", firstFunctionSymbol.SymbolName);
            Assert.Equal(7, firstFunctionSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstFunctionSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference lastVariableSymbol = symbolsResult.Last(r => r.SymbolType == SymbolType.Variable);
            Assert.Equal("$Script:ScriptVar2", lastVariableSymbol.SymbolName);
            Assert.Equal(3, lastVariableSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, lastVariableSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstWorkflowSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Workflow);
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
        public void FindsSymbolsInPesterFile()
        {
            List<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInPesterFile.SourceDetails);
            Assert.Equal(5, symbolsResult.Count);
        }

        [Fact]
        public void LangServerFindsSymbolsInPSDFile()
        {
            List<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInPSDFile.SourceDetails);
            Assert.Equal(3, symbolsResult.Count);
        }

        [Fact]
        public void FindsSymbolsInNoSymbolsFile()
        {
            List<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInNoSymbolsFile.SourceDetails);
            Assert.Empty(symbolsResult);
        }
    }
}
