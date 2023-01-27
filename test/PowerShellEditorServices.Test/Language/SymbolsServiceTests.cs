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
            Assert.Equal("function My-Function($myInput)", symbol.DisplayString);
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
            Assert.Equal("function My-Function($myInput)", symbol.DisplayString);
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
                (i) => AssertIsRegion(i.NameRegion, 1, 10, 1, 21),
                (i) => AssertIsRegion(i.NameRegion, 3, 5, 3, 16),
                (i) => AssertIsRegion(i.NameRegion, 10, 1, 10, 12));
        }

        [Fact]
        public async Task FindsReferencesOnFunctionIncludingAliases()
        {
            // TODO: Same as in FindsFunctionDefinitionForAlias.
            await psesHost.ExecutePSCommandAsync(
                new PSCommand().AddScript("Set-Alias -Name My-Alias -Value My-Function"),
                CancellationToken.None).ConfigureAwait(true);

            IEnumerable<SymbolReference> symbols = await GetReferences(FindsReferencesOnFunctionData.SourceDetails).ConfigureAwait(true);
            Assert.Equal(9, symbols.Count());

            Assert.Collection(symbols.Where((i) => i.FilePath.EndsWith(FindsReferencesOnFunctionData.SourceDetails.File)),
                (i) => AssertIsRegion(i.NameRegion, 1, 10, 1, 21),
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
        public async Task FindsFunctionDefinitionsInWorkspace()
        {
            IEnumerable<SymbolReference> symbols = await GetDefinitions(FindsFunctionDefinitionInDotSourceReferenceData.SourceDetails).ConfigureAwait(true);
            Assert.Collection(symbols.OrderBy((i) => i.FilePath),
                (i) =>
                {
                    Assert.Equal("My-Function", i.SymbolName);
                    Assert.EndsWith("ReferenceFileA.ps1", i.FilePath);
                },
                (i) =>
                {
                    Assert.Equal("My-Function", i.SymbolName);
                    Assert.EndsWith(FindsFunctionDefinitionData.SourceDetails.File, i.FilePath);
                });
        }

        [Fact]
        public async Task FindsFunctionDefinitionInWorkspace()
        {
            SymbolReference symbol = await GetDefinition(FindsFunctionDefinitionInWorkspaceData.SourceDetails).ConfigureAwait(true);
            Assert.EndsWith("ReferenceFileE.ps1", symbol.FilePath);
            Assert.Equal("My-FunctionInFileE", symbol.SymbolName);
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
            Assert.Equal("Second", symbol.SymbolName);
            Assert.Equal("Second", symbol.DisplayString);
            Assert.True(symbol.IsDeclaration);
            AssertIsRegion(symbol.NameRegion, 41, 5, 41, 11);
            Assert.Equal(41, symbol.ScriptRegion.StartLineNumber);
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
        public async Task FindsDetailsWithSignatureForEnumMember()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForTypeSymbolsData.EnumMemberSourceDetails),
                FindsDetailsForTypeSymbolsData.EnumMemberSourceDetails.StartLineNumber,
                FindsDetailsForTypeSymbolsData.EnumMemberSourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("MyEnum.First", symbolDetails.SymbolReference.DisplayString);
        }

        [Fact]
        public async Task FindsDetailsWithSignatureForProperty()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForTypeSymbolsData.PropertySourceDetails),
                FindsDetailsForTypeSymbolsData.PropertySourceDetails.StartLineNumber,
                FindsDetailsForTypeSymbolsData.PropertySourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("string SuperClass.SomePropWithDefault", symbolDetails.SymbolReference.DisplayString);
        }

        [Fact]
        public async Task FindsDetailsWithSignatureForConstructor()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForTypeSymbolsData.ConstructorSourceDetails),
                FindsDetailsForTypeSymbolsData.ConstructorSourceDetails.StartLineNumber,
                FindsDetailsForTypeSymbolsData.ConstructorSourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("SuperClass.SuperClass([string]$name)", symbolDetails.SymbolReference.DisplayString);
        }

        [Fact]
        public async Task FindsDetailsWithSignatureForMethod()
        {
            SymbolDetails symbolDetails = await symbolsService.FindSymbolDetailsAtLocationAsync(
                GetScriptFile(FindsDetailsForTypeSymbolsData.MethodSourceDetails),
                FindsDetailsForTypeSymbolsData.MethodSourceDetails.StartLineNumber,
                FindsDetailsForTypeSymbolsData.MethodSourceDetails.StartColumnNumber).ConfigureAwait(true);

            Assert.Equal("string SuperClass.MyClassMethod([string]$param1, $param2, [int]$param3)", symbolDetails.SymbolReference.DisplayString);
        }

        [Fact]
        public void FindsSymbolsInFile()
        {
            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInMultiSymbolFile.SourceDetails);

            Assert.Equal(7, symbols.Count(symbolReference => symbolReference.SymbolType == SymbolType.Function));
            Assert.Equal(12, symbols.Count(symbolReference => symbolReference.SymbolType == SymbolType.Variable));

            SymbolReference firstFunctionSymbol = symbols.First(r => r.SymbolType == SymbolType.Function);
            Assert.Equal("AFunction", firstFunctionSymbol.SymbolName);
            AssertIsRegion(firstFunctionSymbol.NameRegion, 7, 10, 7, 19);

            SymbolReference lastVariableSymbol = symbols.Last(r => r.SymbolType == SymbolType.Variable);
            Assert.Equal("$param3", lastVariableSymbol.SymbolName);
            AssertIsRegion(lastVariableSymbol.NameRegion, 32, 50, 32, 57);

            SymbolReference firstWorkflowSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Workflow));
            Assert.Equal("AWorkflow", firstWorkflowSymbol.SymbolName);
            AssertIsRegion(firstWorkflowSymbol.NameRegion, 23, 10, 23, 19);

            SymbolReference firstClassSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Class));
            Assert.Equal("AClass", firstClassSymbol.SymbolName);
            AssertIsRegion(firstClassSymbol.NameRegion, 25, 7, 25, 13);

            SymbolReference firstPropertySymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Property));
            Assert.Equal("AClass.AProperty", firstPropertySymbol.SymbolName);
            AssertIsRegion(firstPropertySymbol.NameRegion, 26, 13, 26, 23);

            SymbolReference firstConstructorSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Constructor));
            Assert.Equal("AClass.AClass([string]$AParameter)", firstConstructorSymbol.SymbolName);
            AssertIsRegion(firstConstructorSymbol.NameRegion, 28, 5, 28, 11);

            SymbolReference firstMethodSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Method));
            Assert.Equal("AClass.AMethod([string]$param1, [int]$param2, $param3)", firstMethodSymbol.SymbolName);
            AssertIsRegion(firstMethodSymbol.NameRegion, 32, 11, 32, 18);

            SymbolReference firstEnumSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Enum));
            Assert.Equal("AEnum", firstEnumSymbol.SymbolName);
            AssertIsRegion(firstEnumSymbol.NameRegion, 37, 6, 37, 11);

            SymbolReference firstEnumMemberSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.EnumMember));
            Assert.Equal("AEnum.AValue", firstEnumMemberSymbol.SymbolName);
            AssertIsRegion(firstEnumMemberSymbol.NameRegion, 38, 5, 38, 11);
        }

        [Fact]
        public void FindsSymbolsWithNewLineInFile()
        {
            IEnumerable<SymbolReference> symbols = FindSymbolsInFile(FindSymbolsInNewLineSymbolFile.SourceDetails);

            SymbolReference firstFunctionSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Function));
            Assert.Equal("returnTrue", firstFunctionSymbol.SymbolName);
            AssertIsRegion(firstFunctionSymbol.NameRegion, 2, 1, 2, 11);
            AssertIsRegion(firstFunctionSymbol.ScriptRegion, 1, 1, 4, 2);

            SymbolReference firstClassSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Class));
            Assert.Equal("NewLineClass", firstClassSymbol.SymbolName);
            AssertIsRegion(firstClassSymbol.NameRegion, 7, 1, 7, 13);
            AssertIsRegion(firstClassSymbol.ScriptRegion, 6, 1, 23, 2);

            SymbolReference firstConstructorSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Constructor));
            Assert.Equal("NewLineClass.NewLineClass()", firstConstructorSymbol.SymbolName);
            AssertIsRegion(firstConstructorSymbol.NameRegion, 8, 5, 8, 17);
            AssertIsRegion(firstConstructorSymbol.ScriptRegion, 8, 5, 10, 6);

            SymbolReference firstPropertySymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Property));
            Assert.Equal("NewLineClass.SomePropWithDefault", firstPropertySymbol.SymbolName);
            AssertIsRegion(firstPropertySymbol.NameRegion, 15, 5, 15, 25);
            AssertIsRegion(firstPropertySymbol.ScriptRegion, 12, 5, 15, 40);

            SymbolReference firstMethodSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Method));
            Assert.Equal("NewLineClass.MyClassMethod([MyNewLineEnum]$param1)", firstMethodSymbol.SymbolName);
            AssertIsRegion(firstMethodSymbol.NameRegion, 20, 5, 20, 18);
            AssertIsRegion(firstMethodSymbol.ScriptRegion, 17, 5, 22, 6);

            SymbolReference firstEnumSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.Enum));
            Assert.Equal("MyNewLineEnum", firstEnumSymbol.SymbolName);
            AssertIsRegion(firstEnumSymbol.NameRegion, 26, 1, 26, 14);
            AssertIsRegion(firstEnumSymbol.ScriptRegion, 25, 1, 28, 2);

            SymbolReference firstEnumMemberSymbol =
                Assert.Single(symbols.Where(symbolReference => symbolReference.SymbolType == SymbolType.EnumMember));
            Assert.Equal("MyNewLineEnum.First", firstEnumMemberSymbol.SymbolName);
            AssertIsRegion(firstEnumMemberSymbol.NameRegion, 27, 5, 27, 10);
            AssertIsRegion(firstEnumMemberSymbol.ScriptRegion, 27, 5, 27, 10);
        }

        [SkippableFact]
        public void FindsSymbolsInDSCFile()
        {
            Skip.If(!s_isWindows, "DSC only works properly on Windows.");

            IEnumerable<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInDSCFile.SourceDetails);

            Assert.Single(symbolsResult.Where(symbolReference => symbolReference.SymbolType == SymbolType.Configuration));
            SymbolReference firstConfigurationSymbol = symbolsResult.First(r => r.SymbolType == SymbolType.Configuration);
            Assert.Equal("AConfiguration", firstConfigurationSymbol.SymbolName);
            Assert.Equal(2, firstConfigurationSymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(15, firstConfigurationSymbol.ScriptRegion.StartColumnNumber);
        }

        [Fact]
        public void FindsSymbolsInPesterFile()
        {
            IEnumerable<PesterSymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInPesterFile.SourceDetails).OfType<PesterSymbolReference>();
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
            SymbolReference firstBeforeDiscoverySymbol = symbolsResult.First(r => r.Command == PesterCommandType.BeforeDiscovery);
            Assert.Equal("BeforeDiscovery", firstBeforeDiscoverySymbol.SymbolName);
            Assert.Equal(1, firstBeforeDiscoverySymbol.ScriptRegion.StartLineNumber);
            Assert.Equal(1, firstBeforeDiscoverySymbol.ScriptRegion.StartColumnNumber);

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
            IEnumerable<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInPSDFile.SourceDetails);
            Assert.Equal(3, symbolsResult.Count());
        }

        [Fact]
        public void FindsSymbolsInNoSymbolsFile()
        {
            IEnumerable<SymbolReference> symbolsResult = FindSymbolsInFile(FindSymbolsInNoSymbolsFile.SourceDetails);
            Assert.Empty(symbolsResult);
        }
    }
}
