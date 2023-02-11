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
            Assert.Equal("fn My-Function", symbol.Id);
            Assert.Equal("function My-Function ($myInput)", symbol.Name);
            Assert.Equal(SymbolType.Function, symbol.Type);
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
            Assert.Equal("function My-Function ($myInput)", symbol.Name);
            Assert.Equal(SymbolType.Function, symbol.Type);
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
                Assert.Equal("fn My-Function", i.Id);
                Assert.Equal("function My-Function ($myInput)", i.Name);
                Assert.Equal(SymbolType.Function, i.Type);
                Assert.True(i.IsDeclaration);
            },
            (i) =>
            {
                Assert.Equal("fn My-Function", i.Id);
                Assert.Equal("My-Function", i.Name);
                Assert.Equal(SymbolType.Function, i.Type);
                Assert.EndsWith(FindsFunctionDefinitionInWorkspaceData.SourceDetails.File, i.FilePath);
                Assert.False(i.IsDeclaration);
            },
            (i) =>
            {
                Assert.Equal("fn My-Function", i.Id);
                Assert.Equal("My-Function", i.Name);
                Assert.Equal(SymbolType.Function, i.Type);
                Assert.False(i.IsDeclaration);
            },
            (i) =>
            {
                Assert.Equal("fn My-Function", i.Id);
                Assert.Equal("My-Function", i.Name);
                Assert.Equal(SymbolType.Function, i.Type);
                Assert.False(i.IsDeclaration);
            },
            (i) =>
            {
                Assert.Equal("fn My-Function", i.Id);
                Assert.Equal("$Function:My-Function", i.Name);
                Assert.Equal(SymbolType.Function, i.Type);
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
                    Assert.Equal("fn My-Alias", i.Id);
                },
                (i) => AssertIsRegion(i.NameRegion, 22, 29, 22, 52));
        }

        [Fact]
        public async Task FindsFunctionDefinitionInWorkspace()
        {
            IEnumerable<SymbolReference> symbols = await GetDefinitions(FindsFunctionDefinitionInWorkspaceData.SourceDetails).ConfigureAwait(true);
            SymbolReference symbol = Assert.Single(symbols);
            Assert.Equal("fn My-Function", symbol.Id);
            Assert.Equal("function My-Function ($myInput)", symbol.Name);
            Assert.True(symbol.IsDeclaration);
            Assert.EndsWith(FindsFunctionDefinitionData.SourceDetails.File, symbol.FilePath);
        }

        [Fact]
        public async Task FindsVariableDefinition()
        {
            IEnumerable<SymbolReference> definitions = await GetDefinitions(FindsVariableDefinitionData.SourceDetails).ConfigureAwait(true);
            SymbolReference symbol = Assert.Single(definitions); // Even though it's re-assigned
            Assert.Equal("var things", symbol.Id);
            Assert.Equal("$things", symbol.Name);
            Assert.Equal(SymbolType.Variable, symbol.Type);
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
                    Assert.Equal("var things", i.Id);
                    Assert.Equal("$things", i.Name);
                    Assert.Equal(SymbolType.Variable, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("var things", i.Id);
                    Assert.Equal("$things", i.Name);
                    Assert.Equal(SymbolType.Variable, i.Type);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("var things", i.Id);
                    Assert.Equal("$things", i.Name);
                    Assert.Equal(SymbolType.Variable, i.Type);
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
                    Assert.Equal("fn My-Function", i.Id);
                    Assert.Equal(SymbolType.Function, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("fn My-Function", i.Id);
                    Assert.Equal(SymbolType.Function, i.Type);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("fn My-Function", i.Id);
                    Assert.Equal(SymbolType.Function, i.Type);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("fn My-Function", i.Id);
                    Assert.Equal("$Function:My-Function", i.Name);
                    Assert.Equal(SymbolType.Function, i.Type);
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
                    Assert.Equal("var myInput", i.Id);
                    // TODO: Parameter names need work.
                    Assert.Equal("(parameter) [System.Object]$myInput", i.Name);
                    Assert.Equal(SymbolType.Parameter, i.Type);
                    AssertIsRegion(i.NameRegion, 1, 23, 1, 31);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("var myInput", i.Id);
                    Assert.Equal("$myInput", i.Name);
                    Assert.Equal(SymbolType.Variable, i.Type);
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
                (i) => Assert.Equal("fn Get-ChildItem", i.Id),
                (i) => Assert.Equal("fn gci", i.Id),
                (i) => Assert.Equal("fn dir", i.Id),
                (i) => Assert.Equal("fn Get-ChildItem", i.Id));
        }

        [Fact]
        public async Task FindsClassDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.ClassSourceDetails).ConfigureAwait(true);
            Assert.Equal("type SuperClass", symbol.Id);
            Assert.Equal("class SuperClass { }", symbol.Name);
            Assert.Equal(SymbolType.Class, symbol.Type);
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
                    Assert.Equal("type SuperClass", i.Id);
                    Assert.Equal("class SuperClass { }", i.Name);
                    Assert.Equal(SymbolType.Class, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type SuperClass", i.Id);
                    Assert.Equal("(type) SuperClass", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.ClassSourceDetails));
        }

        [Fact]
        public async Task FindsEnumDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.EnumSourceDetails).ConfigureAwait(true);
            Assert.Equal("type MyEnum", symbol.Id);
            Assert.Equal("enum MyEnum { }", symbol.Name);
            Assert.Equal(SymbolType.Enum, symbol.Type);
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
                    Assert.Equal("type MyEnum", i.Id);
                    Assert.Equal("(type) MyEnum", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type MyEnum", i.Id);
                    Assert.Equal("enum MyEnum { }", i.Name);
                    Assert.Equal(SymbolType.Enum, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type MyEnum", i.Id);
                    Assert.Equal("(type) MyEnum", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type MyEnum", i.Id);
                    Assert.Equal("(type) MyEnum", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.EnumSourceDetails));
        }

        [Fact]
        public async Task FindsTypeExpressionDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.TypeExpressionSourceDetails).ConfigureAwait(true);
            AssertIsRegion(symbol.NameRegion, 39, 6, 39, 12);
            Assert.Equal("type MyEnum", symbol.Id);
            Assert.Equal("enum MyEnum { }", symbol.Name);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public async Task FindsReferencesOnTypeExpression()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.TypeExpressionSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("type SuperClass", i.Id);
                    Assert.Equal("class SuperClass { }", i.Name);
                    Assert.Equal(SymbolType.Class, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type SuperClass", i.Id);
                    Assert.Equal("(type) SuperClass", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.TypeExpressionSourceDetails));
        }

        [Fact]
        public async Task FindsTypeConstraintDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.TypeConstraintSourceDetails).ConfigureAwait(true);
            AssertIsRegion(symbol.NameRegion, 39, 6, 39, 12);
            Assert.Equal("type MyEnum", symbol.Id);
            Assert.Equal("enum MyEnum { }", symbol.Name);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public async Task FindsReferencesOnTypeConstraint()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.TypeConstraintSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("type MyEnum", i.Id);
                    Assert.Equal("(type) MyEnum", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type MyEnum", i.Id);
                    Assert.Equal("enum MyEnum { }", i.Name);
                    Assert.Equal(SymbolType.Enum, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type MyEnum", i.Id);
                    Assert.Equal("(type) MyEnum", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
                    Assert.False(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type MyEnum", i.Id);
                    Assert.Equal("(type) MyEnum", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
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
                    Assert.Equal("type BaseClass", i.Id);
                    Assert.Equal("class BaseClass { }", i.Name);
                    Assert.Equal(SymbolType.Class, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("type BaseClass", i.Id);
                    Assert.Equal("(type) BaseClass", i.Name);
                    Assert.Equal(SymbolType.Type, i.Type);
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
                    Assert.Equal("mtd SuperClass", i.Id);
                    Assert.Equal("SuperClass([string]$name)", i.Name);
                    Assert.Equal(SymbolType.Constructor, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("mtd SuperClass", i.Id);
                    Assert.Equal("SuperClass()", i.Name);
                    Assert.Equal(SymbolType.Constructor, i.Type);
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
                    Assert.Equal("mtd MyClassMethod", i.Id);
                    Assert.Equal("string MyClassMethod([string]$param1, $param2, [int]$param3)", i.Name);
                    Assert.Equal(SymbolType.Method, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("mtd MyClassMethod", i.Id);
                    Assert.Equal("string MyClassMethod([MyEnum]$param1)", i.Name);
                    Assert.Equal(SymbolType.Method, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("mtd MyClassMethod", i.Id);
                    Assert.Equal("string MyClassMethod()", i.Name);
                    Assert.Equal(SymbolType.Method, i.Type);
                    Assert.True(i.IsDeclaration);
                });
        }

        [Fact]
        public async Task FindsReferencesOnMethod()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.MethodSourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) => Assert.Equal("string MyClassMethod([string]$param1, $param2, [int]$param3)", i.Name),
                (i) => Assert.Equal("string MyClassMethod([MyEnum]$param1)", i.Name),
                (i) => Assert.Equal("string MyClassMethod()", i.Name),
                (i) => // The invocation!
                {
                    Assert.Equal("mtd MyClassMethod", i.Id);
                    Assert.Equal("(method) MyClassMethod", i.Name);
                    Assert.Equal("$o.MyClassMethod()", i.SourceLine);
                    Assert.Equal(SymbolType.Method, i.Type);
                    Assert.False(i.IsDeclaration);
                });

            Assert.Equal(symbols, GetOccurrences(FindsOccurrencesOnTypeSymbolsData.MethodSourceDetails));
        }

        [Fact]
        public async Task FindsPropertyDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.PropertySourceDetails).ConfigureAwait(true);
            Assert.Equal("prop SomePropWithDefault", symbol.Id);
            Assert.Equal("[string] $SomePropWithDefault", symbol.Name);
            Assert.Equal(SymbolType.Property, symbol.Type);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public async Task FindsReferencesOnProperty()
        {
            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnTypeSymbolsData.PropertySourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols,
                (i) =>
                {
                    Assert.Equal("prop SomeProp", i.Id);
                    Assert.Equal("[int] $SomeProp", i.Name);
                    Assert.Equal(SymbolType.Property, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("prop SomeProp", i.Id);
                    Assert.Equal("(property) SomeProp", i.Name);
                    Assert.Equal(SymbolType.Property, i.Type);
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
                    Assert.Equal("prop SomePropWithDefault", i.Id);
                    Assert.Equal("[string] $SomePropWithDefault", i.Name);
                    Assert.Equal(SymbolType.Property, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("prop SomePropWithDefault", i.Id);
                    Assert.Equal("(property) SomePropWithDefault", i.Name);
                    Assert.Equal(SymbolType.Property, i.Type);
                    Assert.False(i.IsDeclaration);
                });
        }

        [Fact]
        public async Task FindsEnumMemberDefinition()
        {
            SymbolReference symbol = await GetDefinition(FindsTypeSymbolsDefinitionData.EnumMemberSourceDetails).ConfigureAwait(true);
            Assert.Equal("prop Second", symbol.Id);
            // Doesn't include [MyEnum]:: because that'd be redundant in the outline.
            Assert.Equal("Second", symbol.Name);
            Assert.Equal(SymbolType.EnumMember, symbol.Type);
            Assert.True(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 41, 5, 41, 11);

            symbol = await GetDefinition(FindsReferencesOnTypeSymbolsData.EnumMemberSourceDetails).ConfigureAwait(true);
            Assert.Equal("prop First", symbol.Id);
            Assert.Equal("First", symbol.Name);
            Assert.Equal(SymbolType.EnumMember, symbol.Type);
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
                    Assert.Equal("prop First", i.Id);
                    Assert.Equal("First", i.Name);
                    Assert.Equal(SymbolType.EnumMember, i.Type);
                    Assert.True(i.IsDeclaration);
                },
                (i) =>
                {
                    Assert.Equal("prop First", i.Id);
                    // The reference is just a member invocation, and so indistinguishable from a property.
                    Assert.Equal("(property) First", i.Name);
                    Assert.Equal(SymbolType.Property, i.Type);
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

            Assert.Equal(7, symbols.Count(i => i.Type == SymbolType.Function));
            Assert.Equal(8, symbols.Count(i => i.Type == SymbolType.Variable));
            Assert.Equal(4, symbols.Count(i => i.Type == SymbolType.Parameter));
            Assert.Equal(12, symbols.Count(i => i.Id.StartsWith("var ")));
            Assert.Equal(2, symbols.Count(i => i.Id.StartsWith("prop ")));

            SymbolReference symbol = symbols.First(i => i.Type == SymbolType.Function);
            Assert.Equal("fn AFunction", symbol.Id);
            Assert.Equal("function script:AFunction ()", symbol.Name);
            Assert.True(symbol.IsDeclaration);
            Assert.Equal(2, GetOccurrences(symbol.NameRegion).Count());

            symbol = symbols.First(i => i.Id == "fn AFilter");
            Assert.Equal("filter AFilter ()", symbol.Name);
            Assert.True(symbol.IsDeclaration);

            symbol = symbols.Last(i => i.Type == SymbolType.Variable);
            Assert.Equal("var nestedVar", symbol.Id);
            Assert.Equal("$nestedVar", symbol.Name);
            Assert.False(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 16, 29, 16, 39);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Workflow));
            Assert.Equal("fn AWorkflow", symbol.Id);
            Assert.Equal("workflow AWorkflow ()", symbol.Name);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Class));
            Assert.Equal("type AClass", symbol.Id);
            Assert.Equal("class AClass { }", symbol.Name);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Property));
            Assert.Equal("prop AProperty", symbol.Id);
            Assert.Equal("[string] $AProperty", symbol.Name);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Constructor));
            Assert.Equal("mtd AClass", symbol.Id);
            Assert.Equal("AClass([string]$AParameter)", symbol.Name);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Method));
            Assert.Equal("mtd AMethod", symbol.Id);
            Assert.Equal("void AMethod([string]$param1, [int]$param2, $param3)", symbol.Name);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Enum));
            Assert.Equal("type AEnum", symbol.Id);
            Assert.Equal("enum AEnum { }", symbol.Name);
            Assert.True(symbol.IsDeclaration);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.EnumMember));
            Assert.Equal("prop AValue", symbol.Id);
            Assert.Equal("AValue", symbol.Name);
            Assert.True(symbol.IsDeclaration);
        }

        [Fact]
        public void FindsSymbolsWithNewLineInFile()
        {
            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInNewLineSymbolFile.SourceDetails);

            SymbolReference symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Function));
            Assert.Equal("fn returnTrue", symbol.Id);
            AssertIsRegion(symbol.NameRegion, 2, 1, 2, 11);
            AssertIsRegion(symbol.ScriptRegion, 1, 1, 4, 2);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Class));
            Assert.Equal("type NewLineClass", symbol.Id);
            AssertIsRegion(symbol.NameRegion, 7, 1, 7, 13);
            AssertIsRegion(symbol.ScriptRegion, 6, 1, 23, 2);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Constructor));
            Assert.Equal("mtd NewLineClass", symbol.Id);
            AssertIsRegion(symbol.NameRegion, 8, 5, 8, 17);
            AssertIsRegion(symbol.ScriptRegion, 8, 5, 10, 6);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Property));
            Assert.Equal("prop SomePropWithDefault", symbol.Id);
            AssertIsRegion(symbol.NameRegion, 15, 5, 15, 25);
            AssertIsRegion(symbol.ScriptRegion, 12, 5, 15, 40);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Method));
            Assert.Equal("mtd MyClassMethod", symbol.Id);
            Assert.Equal("string MyClassMethod([MyNewLineEnum]$param1)", symbol.Name);
            AssertIsRegion(symbol.NameRegion, 20, 5, 20, 18);
            AssertIsRegion(symbol.ScriptRegion, 17, 5, 22, 6);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.Enum));
            Assert.Equal("type MyNewLineEnum", symbol.Id);
            AssertIsRegion(symbol.NameRegion, 26, 1, 26, 14);
            AssertIsRegion(symbol.ScriptRegion, 25, 1, 28, 2);

            symbol = Assert.Single(symbols.Where(i => i.Type == SymbolType.EnumMember));
            Assert.Equal("prop First", symbol.Id);
            AssertIsRegion(symbol.NameRegion, 27, 5, 27, 10);
            AssertIsRegion(symbol.ScriptRegion, 27, 5, 27, 10);
        }

        [Fact(Skip="DSC symbols don't work yet.")]
        public void FindsSymbolsInDSCFile()
        {
            Skip.If(!s_isWindows, "DSC only works properly on Windows.");

            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInDSCFile.SourceDetails);
            SymbolReference symbol = Assert.Single(symbols, i => i.Type == SymbolType.Configuration);
            Assert.Equal("AConfiguration", symbol.Id);
            Assert.Equal(2, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(15, symbol.ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsSymbolsInPesterFile()
        {
            IEnumerable<PesterSymbolReference> symbols = FindSymbolsInFile(FindSymbolsInPesterFile.SourceDetails).OfType<PesterSymbolReference>();
            Assert.Equal(12, symbols.Count(i => i.Type == SymbolType.Function));

            SymbolReference symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.Describe);
            Assert.Equal("Describe \"Testing Pester symbols\"", symbol.Id);
            Assert.Equal(9, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.Context);
            Assert.Equal("Context \"When a Pester file is given\"", symbol.Id);
            Assert.Equal(10, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, symbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(4, symbols.Count(i => i.Command == PesterCommandType.It));
            symbol = symbols.Last(i => i.Command == PesterCommandType.It);
            Assert.Equal("It \"Should return setup and teardown symbols\"", symbol.Id);
            Assert.Equal(31, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.BeforeDiscovery);
            Assert.Equal("BeforeDiscovery", symbol.Id);
            Assert.Equal(1, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, symbol.ScriptRegion.StartColumnNumber);

            Assert.Equal(2, symbols.Count(i => i.Command == PesterCommandType.BeforeAll));
            symbol = symbols.Last(i => i.Command == PesterCommandType.BeforeAll);
            Assert.Equal("BeforeAll", symbol.Id);
            Assert.Equal(11, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.BeforeEach);
            Assert.Equal("BeforeEach", symbol.Id);
            Assert.Equal(15, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.AfterEach);
            Assert.Equal("AfterEach", symbol.Id);
            Assert.Equal(35, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(9, symbol.ScriptRegion.StartColumnNumber);

            symbol = Assert.Single(symbols, i => i.Command == PesterCommandType.AfterAll);
            Assert.Equal("AfterAll", symbol.Id);
            Assert.Equal(40, symbol.ScriptRegion.StartLineNumber);
            Assert.Equal(5, symbol.ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsSymbolsInPSKoansFile()
        {
            IEnumerable<PesterSymbolReference> symbols = FindSymbolsInFile(FindSymbolsInPSKoansFile.SourceDetails).OfType<PesterSymbolReference>();

            // Pester symbols are properly tested in FindsSymbolsInPesterFile so only counting to make sure they appear
            Assert.Equal(7, symbols.Count(i => i.Type == SymbolType.Function));
        }

        [Fact]
        public void FindsSymbolsInPSDFile()
        {
            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInPSDFile.SourceDetails);
            Assert.All(symbols, i => Assert.Equal(SymbolType.HashtableKey, i.Type));
            Assert.Collection(symbols,
                i => Assert.Equal("property1", i.Id),
                i => Assert.Equal("property2", i.Id),
                i => Assert.Equal("property3", i.Id));
        }

        [Fact]
        public void FindsSymbolsInNoSymbolsFile()
        {
            IEnumerable<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInNoSymbolsFile.SourceDetails);
            Assert.Empty(symbolsResult);
        }
    }
}
