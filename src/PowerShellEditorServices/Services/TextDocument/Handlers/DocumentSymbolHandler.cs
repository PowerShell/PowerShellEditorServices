// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesDocumentSymbolHandler : DocumentSymbolHandlerBase
    {
        private static readonly SymbolInformationOrDocumentSymbolContainer s_emptySymbolInformationOrDocumentSymbolContainer = new();
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;
        private readonly IDocumentSymbolProvider[] _providers;

        public PsesDocumentSymbolHandler(ILoggerFactory factory, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesDocumentSymbolHandler>();
            _workspaceService = workspaceService;
            _providers = new IDocumentSymbolProvider[]
            {
                new ScriptDocumentSymbolProvider(),
                new PsdDocumentSymbolProvider(),
                new PesterDocumentSymbolProvider(),
                new RegionDocumentSymbolProvider()
            };
        }

        protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        // AKA the outline feature
        public override async Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handling document symbols for {request.TextDocument.Uri}");

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            List<SymbolInformationOrDocumentSymbol> symbols = new();

            foreach (SymbolReference r in ProvideDocumentSymbols(scriptFile))
            {
                // This async method is pretty dense with synchronous code
                // so it's helpful to add some yields.
                await Task.Yield();
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Outline view should only include declarations.
                //
                // TODO: We should also include function invocations that are part of DSLs (like
                // Invoke-Build etc.).
                if (!r.IsDeclaration || r.Type is SymbolType.Parameter)
                {
                    continue;
                }

                symbols.Add(new SymbolInformationOrDocumentSymbol(new DocumentSymbol
                {
                    Kind = SymbolTypeUtils.GetSymbolKind(r.Type),
                    // Jump to name start, but keep whole range to support symbol tree in outline
                    Range = new Range(r.NameRegion.ToRange().Start, r.ScriptRegion.ToRange().End),
                    Name = r.Name
                }));
            }

            return symbols.Count == 0
                ? s_emptySymbolInformationOrDocumentSymbolContainer
                : new SymbolInformationOrDocumentSymbolContainer(symbols);
        }

        private IEnumerable<SymbolReference> ProvideDocumentSymbols(ScriptFile scriptFile)
        {
            foreach (IDocumentSymbolProvider provider in _providers)
            {
                foreach (SymbolReference symbol in provider.ProvideDocumentSymbols(scriptFile))
                {
                    yield return symbol;
                }
            }
        }
    }
}
