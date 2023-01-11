// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Host;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Utility;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Test;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using Microsoft.PowerShell.EditorServices.Test.Shared.Definition;
using Microsoft.PowerShell.EditorServices.Test.Shared.Occurrences;
using Microsoft.PowerShell.EditorServices.Test.Shared.ParameterHint;
using Microsoft.PowerShell.EditorServices.Test.Shared.References;
using Microsoft.PowerShell.EditorServices.Test.Shared.SymbolDetails;
using Microsoft.PowerShell.EditorServices.Test.Shared.Symbols;
using Xunit;

namespace PowerShellEditorServices.Test.Language
{
    [Trait("Category", "Symbols")]
    public class SymbolsServiceTests : IDisposable
    {
        private readonly PsesInternalHost psesHost;
        private readonly WorkspaceService workspace;
        private readonly SymbolsService symbolsService;
        private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

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
#pragma warning disable VSTHRD002
            psesHost.StopAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
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
            // TODO: Eventually we should get the aliases through the AST instead of relying on them
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
            Assert.Equal(2, referencesResult.Count);
            Assert.Equal(3, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(2, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public async Task FindsReferencesOnFunctionIncludingAliases()
        {
            // TODO: Same as in FindsFunctionDefinitionForAlias.
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("Set-Alias -Name My-Alias -Value My-Function"),
                CancellationToken.None).ConfigureAwait(true);

            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnFunctionData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(3, referencesResult.Count);
            Assert.Equal(3, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(2, referencesResult[0].ScriptRegion.StartColumnNumber);
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
            Assert.NotNull(definitionResult);
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
        public void FindsOccurrencesOnVariable()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnVariableData.SourceDetails);
            Assert.Equal(3, occurrencesResult.Count);
            Assert.Equal(10, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
            Assert.Equal(13, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartColumnNumber);
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
            Assert.Equal("Get-ChildItem", referencesResult[1].SymbolName);
            Assert.Equal("Get-ChildItem", referencesResult[2].SymbolName);
            Assert.Equal("Get-ChildItem", referencesResult[referencesResult.Count - 1].SymbolName);
        }

        [Fact]
        public async Task FindsClassDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsTypeSymbolsDefinitionData.ClassSourceDetails).ConfigureAwait(true);
            Assert.Equal(8, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(7, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("SuperClass", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnClass()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnTypeSymbolsData.ClassSourceDetails).ConfigureAwait(true);
            Assert.Equal(2, referencesResult.Count);
            Assert.Equal(8, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(7, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnClass()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.ClassSourceDetails);
            Assert.Equal(2, occurrencesResult.Count);
            Assert.Equal("[SuperClass]", occurrencesResult[occurrencesResult.Count - 1].SymbolName);
            Assert.Equal(34, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsEnumDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsTypeSymbolsDefinitionData.EnumSourceDetails).ConfigureAwait(true);
            Assert.Equal(39, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(6, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("MyEnum", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnEnum()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnTypeSymbolsData.EnumSourceDetails).ConfigureAwait(true);
            Assert.Equal(4, referencesResult.Count);
            Assert.Equal(25, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(19, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnEnum()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.EnumSourceDetails);
            Assert.Equal(4, occurrencesResult.Count);
            Assert.Equal("[MyEnum]", occurrencesResult[occurrencesResult.Count - 1].SymbolName);
            Assert.Equal(46, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsTypeExpressionDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsTypeSymbolsDefinitionData.TypeExpressionSourceDetails).ConfigureAwait(true);
            Assert.Equal(39, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(6, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("MyEnum", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnTypeExpression()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnTypeSymbolsData.TypeExpressionSourceDetails).ConfigureAwait(true);
            Assert.Equal(2, referencesResult.Count);
            Assert.Equal(8, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(7, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnTypeExpression()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.TypeExpressionSourceDetails);
            Assert.Equal(2, occurrencesResult.Count);
            Assert.Equal("SuperClass", occurrencesResult[0].SymbolName);
            Assert.Equal(8, occurrencesResult[0].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsTypeConstraintDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsTypeSymbolsDefinitionData.TypeConstraintSourceDetails).ConfigureAwait(true);
            Assert.Equal(39, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(6, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("MyEnum", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnTypeConstraint()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnTypeSymbolsData.TypeConstraintSourceDetails).ConfigureAwait(true);
            Assert.Equal(4, referencesResult.Count);
            Assert.Equal(25, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(19, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnTypeConstraint()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.TypeConstraintSourceDetails);
            Assert.Equal(2, occurrencesResult.Count);
            Assert.Equal("BaseClass", occurrencesResult[0].SymbolName);
            Assert.Equal(4, occurrencesResult[0].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsConstructorDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsTypeSymbolsDefinitionData.ConstructorSourceDetails).ConfigureAwait(true);
            Assert.Equal(9, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(5, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("SuperClass.SuperClass([string]$name)", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnConstructor()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnTypeSymbolsData.ConstructorSourceDetails).ConfigureAwait(true);
            Assert.Single(referencesResult);
            Assert.Equal(9, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(5, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnConstructor()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.ConstructorSourceDetails);
            Assert.Single(occurrencesResult);
            Assert.Equal("SuperClass.SuperClass()", occurrencesResult[occurrencesResult.Count - 1].SymbolName);
            Assert.Equal(13, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsMethodDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsTypeSymbolsDefinitionData.MethodSourceDetails).ConfigureAwait(true);
            Assert.Equal(19, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(13, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("SuperClass.MyClassMethod([string]$param1, $param2, [int]$param3)", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnMethod()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnTypeSymbolsData.MethodSourceDetails).ConfigureAwait(true);
            Assert.Single(referencesResult);
            Assert.Equal(19, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(13, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnMethod()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.MethodSourceDetails);
            Assert.Single(occurrencesResult);
            Assert.Equal("SuperClass.MyClassMethod()", occurrencesResult[occurrencesResult.Count - 1].SymbolName);
            Assert.Equal(28, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsPropertyDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsTypeSymbolsDefinitionData.PropertySourceDetails).ConfigureAwait(true);
            Assert.Equal(15, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(13, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("SuperClass.SomePropWithDefault", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnProperty()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnTypeSymbolsData.PropertySourceDetails).ConfigureAwait(true);
            Assert.Single(referencesResult);
            Assert.Equal(17, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(10, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnProperty()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.PropertySourceDetails);
            Assert.Equal(1, occurrencesResult.Count);
            Assert.Equal("SuperClass.SomePropWithDefault", occurrencesResult[occurrencesResult.Count - 1].SymbolName);
            Assert.Equal(15, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsEnumMemberDefinition()
        {
            SymbolReference definitionResult = await GetDefinition(FindsTypeSymbolsDefinitionData.EnumMemberSourceDetails).ConfigureAwait(true);
            Assert.Equal(41, definitionResult.ScriptRegion.StartLineNumber);
            Assert.Equal(5, definitionResult.ScriptRegion.StartColumnNumber);
            Assert.Equal("MyEnum.Second", definitionResult.SymbolName);
        }

        [Fact]
        public async Task FindsReferencesOnEnumMember()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnTypeSymbolsData.EnumMemberSourceDetails).ConfigureAwait(true);
            Assert.Single(referencesResult);
            Assert.Equal(41, referencesResult[0].ScriptRegion.StartLineNumber);
            Assert.Equal(5, referencesResult[0].ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsOccurrencesOnEnumMember()
        {
            IReadOnlyList<SymbolReference> occurrencesResult = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.EnumMemberSourceDetails);
            Assert.Single(occurrencesResult);
            Assert.Equal("MyEnum.First", occurrencesResult[occurrencesResult.Count - 1].SymbolName);
            Assert.Equal(40, occurrencesResult[occurrencesResult.Count - 1].ScriptRegion.StartLineNumber);
        }

        [Fact]
        public async Task FindsReferencesOnFileWithReferencesFileB()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnFunctionMultiFileDotSourceFileB.SourceDetails).ConfigureAwait(true);
            Assert.Equal(3, referencesResult.Count);
        }

        [Fact]
        public async Task FindsReferencesOnFileWithReferencesFileC()
        {
            List<SymbolReference> referencesResult = await GetReferences(FindsReferencesOnFunctionMultiFileDotSourceFileC.SourceDetails).ConfigureAwait(true);
            Assert.Equal(3, referencesResult.Count);
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
        public async Task FindsDetailsWithSignatureForEnumMember()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForTypeSymbolsData.EnumMemberSourceDetails),
                FindsDetailsForTypeSymbolsData.EnumMemberSourceDetails.StartLineNumber,
                FindsDetailsForTypeSymbolsData.EnumMemberSourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("MyEnum.First", symbolDetails.DisplayString);
        }

        [Fact]
        public async Task FindsDetailsWithSignatureForProperty()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForTypeSymbolsData.PropertySourceDetails),
                FindsDetailsForTypeSymbolsData.PropertySourceDetails.StartLineNumber,
                FindsDetailsForTypeSymbolsData.PropertySourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("string SuperClass.SomePropWithDefault", symbolDetails.DisplayString);
        }

        [Fact]
        public async Task FindsDetailsWithSignatureForConstructor()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForTypeSymbolsData.ConstructorSourceDetails),
                FindsDetailsForTypeSymbolsData.ConstructorSourceDetails.StartLineNumber,
                FindsDetailsForTypeSymbolsData.ConstructorSourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("SuperClass.SuperClass([string]$name)", symbolDetails.DisplayString);
        }

        [Fact]
        public async Task FindsDetailsWithSignatureForMethod()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForTypeSymbolsData.MethodSourceDetails),
                FindsDetailsForTypeSymbolsData.MethodSourceDetails.StartLineNumber,
                FindsDetailsForTypeSymbolsData.MethodSourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("string SuperClass.MyClassMethod([string]$param1, $param2, [int]$param3)", symbolDetails.DisplayString);
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
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Class));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Property));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Constructor));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Method));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Enum));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.EnumMember));

            SymbolReference firstFunctionSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Function);
            Assert.Equal("AFunction", firstFunctionSymbol.SymbolName);
            Assert.Equal(7, firstFunctionSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(10, firstFunctionSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference lastVariableSymbol = symbolsResult.Last(r => r.SymbolType == SymbolType.Variable);
            Assert.Equal("$Script:ScriptVar2", lastVariableSymbol.SymbolName);
            Assert.Equal(3, lastVariableSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, lastVariableSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstWorkflowSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Workflow);
            Assert.Equal("AWorkflow", firstWorkflowSymbol.SymbolName);
            Assert.Equal(23, firstWorkflowSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(10, firstWorkflowSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstClassSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Class);
            Assert.Equal("AClass", firstClassSymbol.SymbolName);
            Assert.Equal(25, firstClassSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(7, firstClassSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstPropertySymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Property);
            Assert.Equal("AProperty", firstPropertySymbol.SymbolName);
            Assert.Equal(26, firstPropertySymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(13, firstPropertySymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstConstructorSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Constructor);
            Assert.Equal("AClass([string]$AParameter)", firstConstructorSymbol.SymbolName);
            Assert.Equal(28, firstConstructorSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, firstConstructorSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstMethodSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Method);
            Assert.Equal("AMethod([string]$param1, [int]$param2, $param3)", firstMethodSymbol.SymbolName);
            Assert.Equal(32, firstMethodSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(11, firstMethodSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstEnumSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Enum);
            Assert.Equal("AEnum", firstEnumSymbol.SymbolName);
            Assert.Equal(37, firstEnumSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(6, firstEnumSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstEnumMemberSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.EnumMember);
            Assert.Equal("AValue", firstEnumMemberSymbol.SymbolName);
            Assert.Equal(38, firstEnumMemberSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, firstEnumMemberSymbol.ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsSymbolsWithNewLineInFile()
        {
            List<SymbolReference> symbolsResult =
                FindSymbolsInFile(
                    FindSymbolsInNewLineSymbolFile.SourceDetails);

            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Function));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Class));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Constructor));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Property));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Method));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Enum));
            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.EnumMember));

            SymbolReference firstFunctionSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Function);
            Assert.Equal("returnTrue", firstFunctionSymbol.SymbolName);
            Assert.Equal(2, firstFunctionSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstFunctionSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstClassSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Class);
            Assert.Equal("NewLineClass", firstClassSymbol.SymbolName);
            Assert.Equal(7, firstClassSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstClassSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstConstructorSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Constructor);
            Assert.Equal("NewLineClass()", firstConstructorSymbol.SymbolName);
            Assert.Equal(8, firstConstructorSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, firstConstructorSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstPropertySymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Property);
            Assert.Equal("SomePropWithDefault", firstPropertySymbol.SymbolName);
            Assert.Equal(15, firstPropertySymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, firstPropertySymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstMethodSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Method);
            Assert.Equal("MyClassMethod([MyNewLineEnum]$param1)", firstMethodSymbol.SymbolName);
            Assert.Equal(20, firstMethodSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, firstMethodSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstEnumSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Enum);
            Assert.Equal("MyNewLineEnum", firstEnumSymbol.SymbolName);
            Assert.Equal(26, firstEnumSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstEnumSymbol.ScriptRegion.StartColumnNumber);

            SymbolReference firstEnumMemberSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.EnumMember);
            Assert.Equal("First", firstEnumMemberSymbol.SymbolName);
            Assert.Equal(27, firstEnumMemberSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, firstEnumMemberSymbol.ScriptRegion.StartColumnNumber);
        }

        [SkippableFact]
        public void FindsSymbolsInDSCFile()
        {
            Skip.If(!s_isWindows, "DSC only works properly on Windows.");

            List<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInDSCFile.SourceDetails);

            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Configuration));
            SymbolReference firstConfigurationSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Configuration);
            Assert.Equal("AConfiguration", firstConfigurationSymbol.SymbolName);
            Assert.Equal(2, firstConfigurationSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(15, firstConfigurationSymbol.ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsSymbolsInPesterFile()
        {
            List<PesterSymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInPesterFile.SourceDetails).OfType<PesterSymbolReference>().ToList();
            Assert.Equal(12, symbolsResult.Count(r => r.SymbolType == SymbolType.Function));

            Assert.Equal(1, symbolsResult.Count(r => r.Command == PesterCommandType.Describe));
            SymbolReference firstDescribeSymbol = symbolsResult.First(r => r.Command == PesterCommandType.Describe);
            Assert.Equal("Describe \"Testing Pester symbols\"", firstDescribeSymbol.SymbolName);
            Assert.Equal(9, firstDescribeSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstDescribeSymbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(1, symbolsResult.Count(r => r.Command == PesterCommandType.Context));
            SymbolReference firstContextSymbol = symbolsResult.First(r => r.Command == PesterCommandType.Context);
            Assert.Equal("Context \"When a Pester file is given\"", firstContextSymbol.SymbolName);
            Assert.Equal(10, firstContextSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, firstContextSymbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(4, symbolsResult.Count(r => r.Command == PesterCommandType.It));
            SymbolReference lastItSymbol = symbolsResult.Last(r => r.Command == PesterCommandType.It);
            Assert.Equal("It \"Should return setup and teardown symbols\"", lastItSymbol.SymbolName);
            Assert.Equal(31, lastItSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, lastItSymbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(1, symbolsResult.Count(r => r.Command == PesterCommandType.BeforeDiscovery));
            SymbolReference firstBeforeDisocverySymbol = symbolsResult.First(r => r.Command == PesterCommandType.BeforeDiscovery);
            Assert.Equal("BeforeDiscovery", firstBeforeDisocverySymbol.SymbolName);
            Assert.Equal(1, firstBeforeDisocverySymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstBeforeDisocverySymbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(2, symbolsResult.Count(r => r.Command == PesterCommandType.BeforeAll));
            SymbolReference lastBeforeAllSymbol = symbolsResult.Last(r => r.Command == PesterCommandType.BeforeAll);
            Assert.Equal("BeforeAll", lastBeforeAllSymbol.SymbolName);
            Assert.Equal(11, lastBeforeAllSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, lastBeforeAllSymbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(1, symbolsResult.Count(r => r.Command == PesterCommandType.BeforeEach));
            SymbolReference firstBeforeEachSymbol = symbolsResult.First(r => r.Command == PesterCommandType.BeforeEach);
            Assert.Equal("BeforeEach", firstBeforeEachSymbol.SymbolName);
            Assert.Equal(15, firstBeforeEachSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, firstBeforeEachSymbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(1, symbolsResult.Count(r => r.Command == PesterCommandType.AfterEach));
            SymbolReference firstAfterEachSymbol = symbolsResult.First(r => r.Command == PesterCommandType.AfterEach);
            Assert.Equal("AfterEach", firstAfterEachSymbol.SymbolName);
            Assert.Equal(35, firstAfterEachSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, firstAfterEachSymbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(1, symbolsResult.Count(r => r.Command == PesterCommandType.AfterAll));
            SymbolReference firstAfterAllSymbol = symbolsResult.First(r => r.Command == PesterCommandType.AfterAll);
            Assert.Equal("AfterAll", firstAfterAllSymbol.SymbolName);
            Assert.Equal(40, firstAfterAllSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, firstAfterAllSymbol.ScriptRegion.StartColumnNumber);
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
