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

        // This turns a flat list of symbols into a hierarchical list. It's ugly because we're
        // dealing with records and so sadly must slowly copy and replace things whenever need to do
        // a modification, but it seems to work.
        private static async Task<List<DocumentSymbol>> SortDocumentSymbols(List<DocumentSymbol> symbols, CancellationToken cancellationToken)
        {
            // Sort by the start of the symbol definition.
            symbols.Sort((x1, x2) => x1.Range.Start.CompareTo(x2.Range.Start));

            List<DocumentSymbol> parents = new();

            foreach (DocumentSymbol symbol in symbols)
            {
                // This async method is pretty dense with synchronous code
                // so it's helpful to add some yields.
                await Task.Yield();
                if (cancellationToken.IsCancellationRequested)
                {
                    return symbols;
                }

                // Base case.
                if (parents.Count == 0)
                {
                    parents.Add(symbol);
                }
                // Symbol starts after end of last symbol parsed.
                else if (symbol.Range.Start > parents[parents.Count - 1].Range.End)
                {
                    parents.Add(symbol);
                }
                // Find where it fits.
                else
                {
                    for (int i = 0; i < parents.Count; i++)
                    {
                        DocumentSymbol parent = parents[i];
                        if (parent.Range.Start <= symbol.Range.Start && symbol.Range.End <= parent.Range.End)
                        {
                            List<DocumentSymbol> children = new();
                            if (parent.Children is not null)
                            {
                                children.AddRange(parent.Children);
                            }
                            children.Add(symbol);
                            parents[i] = parent with { Children = children };
                            break;
                        }
                    }
                }
            }

            // Recursively sort the children.
            for (int i = 0; i < parents.Count; i++)
            {
                DocumentSymbol parent = parents[i];
                if (parent.Children is not null)
                {
                    List<DocumentSymbol> children = new(parent.Children);
                    children = await SortDocumentSymbols(children, cancellationToken).ConfigureAwait(false);
                    parents[i] = parent with { Children = children };
                }
            }

            return parents;
        }

        // AKA the outline feature
        public override async Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handling document symbols for {request.TextDocument.Uri}");

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            List<DocumentSymbol> symbols = new();

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

                // TODO: This now needs the Children property filled out to support hierarchical
                // symbols, and we don't have the information nor algorithm to do that currently.
                // OmniSharp was previously doing this for us based on the range, perhaps we can
                // find that logic and reuse it.
                symbols.Add(new DocumentSymbol
                {
                    Kind = SymbolTypeUtils.GetSymbolKind(r.Type),
                    Range = r.ScriptRegion.ToRange(),
                    SelectionRange = r.NameRegion.ToRange(),
                    Name = r.Name
                });
            }

            // Short-circuit if we have no symbols.
            if (symbols.Count == 0)
            {
                return s_emptySymbolInformationOrDocumentSymbolContainer;
            }

            // Otherwise slowly sort them into a hierarchy.
            symbols = await SortDocumentSymbols(symbols, cancellationToken).ConfigureAwait(false);

            // And finally convert them to the silly SymbolInformationOrDocumentSymbol wrapper.
            List<SymbolInformationOrDocumentSymbol> container = new();
            foreach (DocumentSymbol symbol in symbols)
            {
                container.Add(new SymbolInformationOrDocumentSymbol(symbol));
            }
            return container;
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
