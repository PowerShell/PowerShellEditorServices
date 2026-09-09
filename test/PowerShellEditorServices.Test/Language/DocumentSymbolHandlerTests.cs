// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PowerShell.EditorServices.Handlers;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Test.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace PowerShellEditorServices.Test.Language
{
    [Trait("Category", "Symbols")]
    public class DocumentSymbolHandlerTests
    {
        private readonly WorkspaceService workspace;
        private readonly ConfigurationService configuration;
        private readonly PsesDocumentSymbolHandler documentSymbolHandler;

        public DocumentSymbolHandlerTests()
        {
            workspace = new WorkspaceService(NullLoggerFactory.Instance);
            configuration = new ConfigurationService();
            documentSymbolHandler = new PsesDocumentSymbolHandler(
                NullLoggerFactory.Instance,
                workspace,
                configuration);
        }

        [Fact]
        public async Task ExcludesParametersWhenParameterOutlineIsDisabled()
        {
            configuration.CurrentSettings.EnableParameterOutline = false;

            string filePath = TestUtilities.GetSharedPath(
                "Symbols/DocumentSymbolParameters.ps1");

            SymbolInformationOrDocumentSymbolContainer result =
                await documentSymbolHandler.Handle(
                    new DocumentSymbolParams
                    {
                        TextDocument = new TextDocumentIdentifier(
                            DocumentUri.FromFileSystemPath(filePath))
                    },
                    CancellationToken.None);

            DocumentSymbol function = result
                .Select(symbol => symbol.DocumentSymbol)
                .Single(symbol => symbol.Name.Contains("New-MyFunc"));

            Assert.DoesNotContain(
                function.Children,
                symbol => symbol.Name.Contains("$MyString"));

            Assert.DoesNotContain(
                function.Children,
                symbol => symbol.Name.Contains("$MyLooselyTypedParam"));
        }

        [Fact]
        public async Task IncludesParametersWhenParameterOutlineIsEnabled()
        {
            configuration.CurrentSettings.EnableParameterOutline = true;

            string filePath = TestUtilities.GetSharedPath(
                "Symbols/DocumentSymbolParameters.ps1");

            SymbolInformationOrDocumentSymbolContainer result =
                await documentSymbolHandler.Handle(
                    new DocumentSymbolParams
                    {
                        TextDocument = new TextDocumentIdentifier(
                            DocumentUri.FromFileSystemPath(filePath))
                    },
                    CancellationToken.None);

            DocumentSymbol function = result
                .Select(symbol => symbol.DocumentSymbol)
                .Single(symbol => symbol.Name.Contains("New-MyFunc"));

            Assert.Contains(
                function.Children,
                symbol => symbol.Name.Contains("$MyString"));

            Assert.Contains(
                function.Children,
                symbol => symbol.Name.Contains("$MyLooselyTypedParam"));
        }
    }
}