// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
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
using Microsoft.PowerShell.EditorServices.Utility;
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
            workspace = new WorkspaceService(NullLoggerFactory.Instance)
            {
                WorkspacePath = TestUtilities.GetSharedPath("References")
            };
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

        private static void AssertIsRegion(
            ScriptRegion region,
            int startLineNumber,
            int startColumnNumber,
            int endLineNumber,
            int endColumnNumber)
        {
            Assert.Equal(startLineNumber, region.StartLineNumber);
            Assert.Equal(startColumnNumber, region.StartColumnNumber);
            Assert.Equal(endLineNumber, region.EndLineNumber);
            Assert.Equal(endColumnNumber, region.EndColumnNumber);
        }

        private ScriptFile GetScriptFile(ScriptRegion scriptRegion) => workspace.GetFile(TestUtilities.GetSharedPath(scriptRegion.File));

        private Task<ParameterSetSignatures> GetParamSetSignatures(ScriptRegion scriptRegion)
        {
            return symbolsService.FindParameterSetsInFileAsync(
                GetScriptFile(scriptRegion),
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber);
        }

        private async Task<IEnumerable<SymbolReference>> GetDefinitions(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            // TODO: We should just use the name to find it.
            SymbolReference symbol = SymbolsService.FindSymbolAtLocation(
                scriptFile,
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber);

            Assert.NotNull(symbol);

            IEnumerable<SymbolReference> symbols =
                await symbolsService.GetDefinitionOfSymbolAsync(scriptFile, symbol).ConfigureAwait(true);

            return symbols.OrderBy((i) => i.ScriptRegion.ToRange().Start);
        }

        private async Task<SymbolReference> GetDefinition(ScriptRegion scriptRegion)
        {
            IEnumerable<SymbolReference> definitions = await GetDefinitions(scriptRegion).ConfigureAwait(true);
            return definitions.FirstOrDefault();
        }

        private async Task<IEnumerable<SymbolReference>> GetReferences(ScriptRegion scriptRegion)
        {
            ScriptFile scriptFile = GetScriptFile(scriptRegion);

            SymbolReference symbol = SymbolsService.FindSymbolAtLocation(
                scriptFile,
                scriptRegion.StartLineNumber,
                scriptRegion.StartColumnNumber);

            Assert.NotNull(symbol);

            IEnumerable<SymbolReference> symbols =
                await symbolsService.ScanForReferencesOfSymbolAsync(symbol).ConfigureAwait(true);

            return symbols.OrderBy((i) => i.ScriptRegion.ToRange().Start);
        }

        private IEnumerable<SymbolReference> GetOccurrences(ScriptRegion scriptRegion)
        {
            return SymbolsService
                .FindOccurrencesInFile(
                    GetScriptFile(scriptRegion),
                    scriptRegion.StartLineNumber,
                    scriptRegion.StartColumnNumber)
                .OrderBy(symbol => symbol.ScriptRegion.ToRange().Start)
                .ToArray();
        }

        private IEnumerable<SymbolReference> FindSymbolsInFile(ScriptRegion scriptRegion)
        {
            return symbolsService
                .FindSymbolsInFile(GetScriptFile(scriptRegion))
                .OrderBy(symbol => symbol.ScriptRegion.ToRange().Start);
        }

        [Fact]
        public async Task FindsParameterHintsOnCommand()
        {
            // TODO: Fix signatures to use parameters, not sets.
            ParameterSetSignatures signatures = await GetParamSetSignatures(FindsParameterSetsOnCommandData.SourceDetails).ConfigureAwait(true);
            Assert.NotNull(signatures);
            Assert.Equal("Get-Process", signatures.CommandName);
            Assert.Equal(6, signatures.Signatures.Length);
        }

        [Fact]
        public async Task FindsCommandForParamHintsWithSpaces()
        {
            ParameterSetSignatures signatures = await GetParamSetSignatures(FindsParameterSetsOnCommandWithSpacesData.SourceDetails).ConfigureAwait(true);
            Assert.NotNull(signatures);
            Assert.Equal("Write-Host", signatures.CommandName);
            Assert.Single(signatures.Signatures);
        }

        [Fact]
        public async Task FindsFunctionDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsFunctionDefinitionData.SourceDetails).ConfigureAwait(true);
            Assert.Equal("My-Function", symbol.SymbolName);
            Assert.Equal("function My-Function ($myInput)", symbol.DisplayString);
            Assert.Equal(SymbolType.Function, symbol.SymbolType);
            AssertIsRegion(symbol.NameRegion, 1, 10, 1, 21);
            AssertIsRegion(symbol.ScriptRegion, 1, 1, 4, 2);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public async Task FindsFunctionDefinitionForAlias()
        {
            // TODO: Eventually we should get the aliases through the AST instead of relying on them
            // being defined in the runspace.
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("Set-Alias -Name My-Alias -Value My-Function"),
                CancellationToken.None).ConfigureAwait(true);

            SymbolReference symbol = await GetDefinition(FindsFunctionDefinitionOfAliasData.SourceDetails).ConfigureAwait(true);
            Assert.Equal("function My-Function ($myInput)", symbol.DisplayString);
            Assert.Equal(SymbolType.Function, symbol.SymbolType);
            AssertIsRegion(symbol.NameRegion, 1, 10, 1, 21);
            AssertIsRegion(symbol.ScriptRegion, 1, 1, 4, 2);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public async Task FindsReferencesOnFunction()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnFunctionData.SourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
            (i) =>
            {
                Assert.Equal("My-Function", i.SymbolName);
                Assert.Equal("function My-Function ($myInput)", i.DisplayString);
                Assert.Equal(SymbolType.Function, i.SymbolType);
                Assert.True(i.IsDeclaration);
            },
            (i) =>
            {
                Assert.Equal("My-Function", i.SymbolName);
                Assert.Equal("My-Function", i.DisplayString);
                Assert.Equal(SymbolType.Function, i.SymbolType);
                Assert.EndsWith(FindsFunctionDefinitionInWorkspaceData.SourceDetails.File, i.FilePath);
                Assert.False(i.IsDeclaration);
            },
            (i) =>
            {
                Assert.Equal("My-Function", i.SymbolName);
                Assert.Equal("My-Function", i.DisplayString);
                Assert.Equal(SymbolType.Function, i.SymbolType);
                Assert.False(i.IsDeclaration);
            },
            (i) =>
            {
                Assert.Equal("My-Function", i.SymbolName);
                Assert.Equal("My-Function", i.DisplayString);
                Assert.Equal(SymbolType.Function, i.SymbolType);
                Assert.False(i.IsDeclaration);
            });
        }

        [Fact]
        public async Task FindsReferencesOnFunctionIncludingAliases()
        {
            // TODO: Same as in FindsFunctionDefinitionForAlias.
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("Set-Alias -Name My-Alias -Value My-Function"),
                CancellationToken.None).ConfigureAwait(true);

            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnFunctionData.SourceDetails).ConfigureAwait(true);

            Assert.Collection(symbols,
                (i) => AssertIsRegion(i.NameRegion, 1, 10, 1, 21),
                (i) => AssertIsRegion(i.NameRegion, 3, 1, 3, 12),
                (i) => AssertIsRegion(i.NameRegion, 3, 5, 3, 16),
                (i) => AssertIsRegion(i.NameRegion, 10, 1, 10, 12),
                // The alias.
                (i) =>
                {
                    AssertIsRegion(i.NameRegion, 20, 1, 20, 9);
                    Assert.Equal("My-Alias", i.SymbolName);
                });
        }

        [Fact]
        public async Task FindsFunctionDefinitionInWorkspace()
        {
            IEnumerable<SymbolReference> symbols = await GetDefinitions(FindsFunctionDefinitionInWorkspaceData.SourceDetails).ConfigureAwait(true);
            SymbolReference symbol = Assert.Single(symbols);
            Assert.Equal("My-Function", symbol.SymbolName);
            Assert.Equal("function My-Function ($myInput)", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);
            Assert.EndsWith(FindsFunctionDefinitionData.SourceDetails.File, symbol.FilePath);
        }

        [Fact]
        public async Task FindsVariableDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsVariableDefinitionData.SourceDetails).ConfigureAwait(true);
            Assert.Equal("$things", symbol.SymbolName);
            Assert.Equal("$things", symbol.DisplayString);
            Assert.Equal(SymbolType.Variable, symbol.SymbolType);
            Assert.True(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 6, 1, 6, 8);
        }

        [Fact]
        public async Task FindsReferencesOnVariable()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnVariableData.SourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("$things", i.SymbolName);
                    Assert.Equal(SymbolType.Variable, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("$things", i.SymbolName);
                    Assert.Equal(SymbolType.Variable, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("$things", i.SymbolName);
                    Assert.Equal(SymbolType.Variable, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnVariableData.SourceDetails));
        }

        [Fact]
        public void FindsOccurrencesOnFunction()
        {
            IEnumerable<SymbolReference> symbols = GetOccurrences(FindsOccurrencesOnFunctionData.SourceDetails);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("My-Function", i.SymbolName);
                    Assert.Equal(SymbolType.Function, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("My-Function", i.SymbolName);
                    Assert.Equal(SymbolType.Function, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("My-Function", i.SymbolName);
                    Assert.Equal(SymbolType.Function, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });
        }

        [Fact]
        public void FindsOccurrencesOnParameter()
        {
            IEnumerable<SymbolReference> symbols = GetOccurrences(FindOccurrencesOnParameterData.SourceDetails);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("$myInput", i.SymbolName);
                    // TODO: Parameter display strings need work.
                    Assert.Equal("(parameter) [System.Object]$myInput", i.DisplayString);
                    Assert.Equal(SymbolType.Parameter, i.SymbolType);
                    AssertIsRegion(i.NameRegion, 1, 23, 1, 31);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("$myInput", i.SymbolName);
                    Assert.Equal(SymbolType.Variable, i.SymbolType);
                    AssertIsRegion(i.NameRegion, 3, 17, 3, 25);
                    Assert.False(i.IsDeclaration);
                });
        }

        [Fact]
        public async Task FindsReferencesOnCommandWithAlias()
        {
            // NOTE: This doesn't use GetOccurrences as it's testing for aliases.
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnBuiltInCommandWithAliasData.SourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols.Where(
                (i) => i.FilePath
                        .EndsWith(FindsReferencesOnBuiltInCommandWithAliasData.SourceDetails.File)),
                (i) => Assert.Equal("Get-ChildItem", i.SymbolName),
                (i) => Assert.Equal("gci", i.SymbolName),
                (i) => Assert.Equal("dir", i.SymbolName),
                (i) => Assert.Equal("Get-ChildItem", i.SymbolName));
        }

        [Fact]
        public async Task FindsClassDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.ClassSourceDetails).ConfigureAwait(true);
            Assert.Equal("SuperClass", symbol.SymbolName);
            Assert.Equal("class SuperClass { }", symbol.DisplayString);
            Assert.Equal(SymbolType.Class, symbol.SymbolType);
            Assert.True(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 8, 7, 8, 17);
        }

        [Fact]
        public async Task FindsReferencesOnClass()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.ClassSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("SuperClass", i.SymbolName);
                    Assert.Equal("class SuperClass { }", i.DisplayString);
                    Assert.Equal(SymbolType.Class, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("SuperClass", i.SymbolName);
                    Assert.Equal("(type) SuperClass", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.ClassSourceDetails));
        }

        [Fact]
        public async Task FindsEnumDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.EnumSourceDetails).ConfigureAwait(true);
            Assert.Equal("MyEnum", symbol.SymbolName);
            Assert.Equal("enum MyEnum { }", symbol.DisplayString);
            Assert.Equal(SymbolType.Enum, symbol.SymbolType);
            Assert.True(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 39, 6, 39, 12);
        }

        [Fact]
        public async Task FindsReferencesOnEnum()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.EnumSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("MyEnum", i.SymbolName);
                    Assert.Equal("(type) MyEnum", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("MyEnum", i.SymbolName);
                    Assert.Equal("enum MyEnum { }", i.DisplayString);
                    Assert.Equal(SymbolType.Enum, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("MyEnum", i.SymbolName);
                    Assert.Equal("(type) MyEnum", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("MyEnum", i.SymbolName);
                    Assert.Equal("(type) MyEnum", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.EnumSourceDetails));
        }

        [Fact]
        public async Task FindsTypeExpressionDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.TypeExpressionSourceDetails).ConfigureAwait(true);
            AssertIsRegion(symbol.NameRegion, 39, 6, 39, 12);
            Assert.Equal("MyEnum", symbol.SymbolName);
            Assert.Equal("enum MyEnum { }", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public async Task FindsReferencesOnTypeExpression()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.TypeExpressionSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("SuperClass", i.SymbolName);
                    Assert.Equal("class SuperClass { }", i.DisplayString);
                    Assert.Equal(SymbolType.Class, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("SuperClass", i.SymbolName);
                    Assert.Equal("(type) SuperClass", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.TypeExpressionSourceDetails));
        }

        [Fact]
        public async Task FindsTypeConstraintDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.TypeConstraintSourceDetails).ConfigureAwait(true);
            AssertIsRegion(symbol.NameRegion, 39, 6, 39, 12);
            Assert.Equal("MyEnum", symbol.SymbolName);
            Assert.Equal("enum MyEnum { }", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public async Task FindsReferencesOnTypeConstraint()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.TypeConstraintSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("MyEnum", i.SymbolName);
                    Assert.Equal("(type) MyEnum", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("MyEnum", i.SymbolName);
                    Assert.Equal("enum MyEnum { }", i.DisplayString);
                    Assert.Equal(SymbolType.Enum, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("MyEnum", i.SymbolName);
                    Assert.Equal("(type) MyEnum", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("MyEnum", i.SymbolName);
                    Assert.Equal("(type) MyEnum", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });
        }

        [Fact]
        public void FindsOccurrencesOnTypeConstraint()
        {
            IEnumerable<SymbolReference> symbols = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.TypeConstraintSourceDetails);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("BaseClass", i.SymbolName);
                    Assert.Equal("class BaseClass { }", i.DisplayString);
                    Assert.Equal(SymbolType.Class, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("BaseClass", i.SymbolName);
                    Assert.Equal("(type) BaseClass", i.DisplayString);
                    Assert.Equal(SymbolType.Type, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });
        }

        [Fact]
        public async Task FindsConstructorDefinition()
        {
            IEnumerable<SymbolReference> symbols = await GetDefinitions(FindsTypeSymbolsDefinitionData.ConstructorSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("SuperClass", i.SymbolName);
                    Assert.Equal("SuperClass([string]$name)", i.DisplayString);
                    Assert.Equal(SymbolType.Constructor, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("SuperClass", i.SymbolName);
                    Assert.Equal("SuperClass()", i.DisplayString);
                    Assert.Equal(SymbolType.Constructor, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                });

            Assert.Equal(symbols, await GetReferences(FindsReferencesOnTypeSymbolsData.ConstructorSourceDetails).ConfigureAwait(true));
            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.ConstructorSourceDetails));
        }

        [Fact]
        public async Task FindsMethodDefinition()
        {
            IEnumerable<SymbolReference> symbols = await GetDefinitions(FindsTypeSymbolsDefinitionData.MethodSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("MyClassMethod", i.SymbolName);
                    Assert.Equal("string MyClassMethod([string]$param1, $param2, [int]$param3)", i.DisplayString);
                    Assert.Equal(SymbolType.Method, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("MyClassMethod", i.SymbolName);
                    Assert.Equal("string MyClassMethod([MyEnum]$param1)", i.DisplayString);
                    Assert.Equal(SymbolType.Method, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("MyClassMethod", i.SymbolName);
                    Assert.Equal("string MyClassMethod()", i.DisplayString);
                    Assert.Equal(SymbolType.Method, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                });
        }

        [Fact]
        public async Task FindsReferencesOnMethod()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.MethodSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) => Assert.Equal("string MyClassMethod([string]$param1, $param2, [int]$param3)", i.DisplayString),
                (i) => Assert.Equal("string MyClassMethod([MyEnum]$param1)", i.DisplayString),
                (i) => Assert.Equal("string MyClassMethod()", i.DisplayString),
                (i) => // The invocation!
                {
                    Assert.Equal("MyClassMethod", i.SymbolName);
                    Assert.Equal("(method) MyClassMethod", i.DisplayString);
                    Assert.Equal("$o.MyClassMethod()", i.SourceLine);
                    Assert.Equal(SymbolType.Method, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.MethodSourceDetails));
        }

        [Theory]
        [InlineData(SymbolType.Class, SymbolType.Type)]
        [InlineData(SymbolType.Enum, SymbolType.Type)]
        [InlineData(SymbolType.EnumMember, SymbolType.Property)]
        [InlineData(SymbolType.Variable, SymbolType.Parameter)]
        internal void SymbolTypeEquivalencies(SymbolType left, SymbolType right)
        {
            // When checking if a symbol's type is the "same" we use this utility method which
            // semantically equates the above theory, since for the purposes of narrowing down
            // matching symbols, these types are equivalent.
            Assert.NotEqual(left, right);
            Assert.True(SymbolTypeUtils.SymbolTypeMatches(left, right));
        }

        [Fact]
        public async Task FindsPropertyDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.PropertySourceDetails).ConfigureAwait(true);
            Assert.Equal("$SomePropWithDefault", symbol.SymbolName);
            Assert.Equal("[string] $SomePropWithDefault", symbol.DisplayString);
            Assert.Equal(SymbolType.Property, symbol.SymbolType);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public async Task FindsReferencesOnProperty()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.PropertySourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("$SomeProp", i.SymbolName);
                    Assert.Equal("[int] $SomeProp", i.DisplayString);
                    Assert.Equal(SymbolType.Property, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("$SomeProp", i.SymbolName);
                    Assert.Equal("(property) SomeProp", i.DisplayString);
                    Assert.Equal(SymbolType.Property, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });
        }

        [Fact]
        public void FindsOccurrencesOnProperty()
        {
            IEnumerable<SymbolReference> symbols = GetOccurrences(FindsOccurrencesOnTypeSymbolsData.PropertySourceDetails);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("$SomePropWithDefault", i.SymbolName);
                    Assert.Equal("[string] $SomePropWithDefault", i.DisplayString);
                    Assert.Equal(SymbolType.Property, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("$SomePropWithDefault", i.SymbolName);
                    Assert.Equal("(property) SomePropWithDefault", i.DisplayString);
                    Assert.Equal(SymbolType.Property, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });
        }

        [Fact]
        public async Task FindsEnumMemberDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.EnumMemberSourceDetails).ConfigureAwait(true);
            Assert.Equal("$Second", symbol.SymbolName);
            // Doesn't include [MyEnum]:: because that'd be redundant in the outline.
            Assert.Equal("Second", symbol.DisplayString);
            Assert.Equal(SymbolType.EnumMember, symbol.SymbolType);
            Assert.True(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 41, 5, 41, 11);

            symbol = await GetDefinition(FindsReferencesOnTypeSymbolsData.EnumMemberSourceDetails).ConfigureAwait(true);
            Assert.Equal("$First", symbol.SymbolName);
            Assert.Equal("First", symbol.DisplayString);
            Assert.Equal(SymbolType.EnumMember, symbol.SymbolType);
            Assert.True(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 40, 5, 40, 10);
        }

        [Fact]
        public async Task FindsReferencesOnEnumMember()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.EnumMemberSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("$First", i.SymbolName);
                    Assert.Equal("First", i.DisplayString);
                    Assert.Equal(SymbolType.EnumMember, i.SymbolType);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("$First", i.SymbolName);
                    // The reference is just a member invocation, and so indistinguishable from a property.
                    Assert.Equal("(property) First", i.DisplayString);
                    Assert.Equal(SymbolType.Property, i.SymbolType);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.EnumMemberSourceDetails));
        }

        [SkippableFact]
        public async Task FindsDetailsForBuiltInCommand()
        {
            Skip.IfNot(VersionUtils.IsMacOS, "macOS gets the right synopsis but others don't.");
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForBuiltInCommandData.SourceDetails),
                FindsDetailsForBuiltInCommandData.SourceDetails.StartLineNumber,
                FindsDetailsForBuiltInCommandData.SourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("Gets the processes that are running on the local computer.", symbolDetails.Documentation);
        }

        [Fact]
        public void FindsSymbolsInFile()
        {
            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInMultiSymbolFile.SourceDetails);

            Assert.Equal(7, symbols.Count(i => i.SymbolType == SymbolType.Function));
            Assert.Equal(8, symbols.Count(i => i.SymbolType == SymbolType.Variable));
            Assert.Equal(4, symbols.Count(i => i.SymbolType == SymbolType.Parameter));
            Assert.Equal(12, symbols.Count(i => SymbolTypeUtils.SymbolTypeMatches(SymbolType.Variable, i.SymbolType)));

            SymbolReference symbol = symbols.First(i => i.SymbolType == SymbolType.Function);
            Assert.Equal("AFunction", symbol.SymbolName);
            Assert.Equal("function AFunction ()", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);

            symbol = symbols.First(i => i.SymbolName == "AFilter");
            Assert.Equal("filter AFilter ()", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);

            symbol = symbols.Last(i => i.SymbolType == SymbolType.Variable);
            Assert.Equal("$nestedVar", symbol.SymbolName);
            Assert.Equal("$nestedVar", symbol.DisplayString);
            Assert.False(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 16, 29, 16, 39);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Workflow));
            Assert.Equal("AWorkflow", symbol.SymbolName);
            Assert.Equal("workflow AWorkflow ()", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Class));
            Assert.Equal("AClass", symbol.SymbolName);
            Assert.Equal("class AClass { }", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Property));
            Assert.Equal("$AProperty", symbol.SymbolName);
            Assert.Equal("[string] $AProperty", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Constructor));
            Assert.Equal("AClass", symbol.SymbolName);
            Assert.Equal("AClass([string]$AParameter)", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Method));
            Assert.Equal("AMethod", symbol.SymbolName);
            Assert.Equal("void AMethod([string]$param1, [int]$param2, $param3)", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Enum));
            Assert.Equal("AEnum", symbol.SymbolName);
            Assert.Equal("enum AEnum { }", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.EnumMember));
            Assert.Equal("$AValue", symbol.SymbolName);
            Assert.Equal("AValue", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public void FindsSymbolsWithNewLineInFile()
        {
            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInNewLineSymbolFile.SourceDetails);

            SymbolReference symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Function));
            Assert.Equal("returnTrue", symbol.SymbolName);
            AssertIsRegion(symbol.NameRegion, 2, 1, 2, 11);
            AssertIsRegion(symbol.ScriptRegion, 1, 1, 4, 2);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Class));
            Assert.Equal("NewLineClass", symbol.SymbolName);
            AssertIsRegion(symbol.NameRegion, 7, 1, 7, 13);
            AssertIsRegion(symbol.ScriptRegion, 6, 1, 23, 2);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Constructor));
            Assert.Equal("NewLineClass", symbol.SymbolName);
            AssertIsRegion(symbol.NameRegion, 8, 5, 8, 17);
            AssertIsRegion(symbol.ScriptRegion, 8, 5, 10, 6);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Property));
            Assert.Equal("$SomePropWithDefault", symbol.SymbolName);
            AssertIsRegion(symbol.NameRegion, 15, 5, 15, 25);
            AssertIsRegion(symbol.ScriptRegion, 12, 5, 15, 40);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Method));
            Assert.Equal("MyClassMethod", symbol.SymbolName);
            Assert.Equal("string MyClassMethod([MyNewLineEnum]$param1)", symbol.DisplayString);
            AssertIsRegion(symbol.NameRegion, 20, 5, 20, 18);
            AssertIsRegion(symbol.ScriptRegion, 17, 5, 22, 6);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.Enum));
            Assert.Equal("MyNewLineEnum", symbol.SymbolName);
            AssertIsRegion(symbol.NameRegion, 26, 1, 26, 14);
            AssertIsRegion(symbol.ScriptRegion, 25, 1, 28, 2);

            symbol = Assert.Single(symbols.Where(i => i.SymbolType == SymbolType.EnumMember));
            Assert.Equal("$First", symbol.SymbolName);
            AssertIsRegion(symbol.NameRegion, 27, 5, 27, 10);
            AssertIsRegion(symbol.ScriptRegion, 27, 5, 27, 10);
        }

        [SkippableFact]
        public void FindsSymbolsInDSCFile()
        {
            Skip.If(!s_isWindows, "DSC only works properly on Windows.");

            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInDSCFile.SourceDetails);
            SymbolReference symbol = Assert.Single(symbols, i => i.SymbolType == SymbolType.Configuration);
            Assert.Equal("AConfiguration", symbol.SymbolName);
            Assert.Equal(2, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(15, symbol.ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsSymbolsInPesterFile()
        {
            IEnumerable<PesterSymbolReference> symbols = FindSymbolsInFile(FindSymbolsInPesterFile.SourceDetails).OfType<PesterSymbolReference>();
            Assert.Equal(12, symbols.Count(i => i.SymbolType == SymbolType.Function));

            SymbolReference symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.Describe);
            Assert.Equal("Describe \"Testing Pester symbols\"", symbol.SymbolName);
            Assert.Equal(9, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.Context);
            Assert.Equal("Context \"When a Pester file is given\"", symbol.SymbolName);
            Assert.Equal(10, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, symbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(4, symbols.Count(i => i.Command == PesterCommandType.It));
            symbol = symbols.Last(i => i.Command == PesterCommandType.It);
            Assert.Equal("It \"Should return setup and teardown symbols\"", symbol.SymbolName);
            Assert.Equal(31, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.BeforeDiscovery);
            Assert.Equal("BeforeDiscovery", symbol.SymbolName);
            Assert.Equal(1, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, symbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(2, symbols.Count(i => i.Command == PesterCommandType.BeforeAll));
            symbol = symbols.Last(i => i.Command == PesterCommandType.BeforeAll);
            Assert.Equal("BeforeAll", symbol.SymbolName);
            Assert.Equal(11, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.BeforeEach);
            Assert.Equal("BeforeEach", symbol.SymbolName);
            Assert.Equal(15, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.AfterEach);
            Assert.Equal("AfterEach", symbol.SymbolName);
            Assert.Equal(35, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.AfterAll);
            Assert.Equal("AfterAll", symbol.SymbolName);
            Assert.Equal(40, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, symbol.ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsSymbolsInPSDFile()
        {
            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInPSDFile.SourceDetails);
            Assert.All(symbols, i => Assert.Equal(SymbolType.HashtableKey, i.SymbolType));
            Assert.Collection(symbols,
                i => Assert.Equal("property1", i.SymbolName),
                i => Assert.Equal("property2", i.SymbolName),
                i => Assert.Equal("property3", i.SymbolName));
        }

        [Fact]
        public void FindsSymbolsInNoSymbolsFile()
        {
            IEnumerable<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInNoSymbolsFile.SourceDetails);
            Assert.Empty(symbolsResult);
        }
    }
}
