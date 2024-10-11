// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
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

        // Modifies a flat list of symbols into a hierarchical list.
        private static Task SortHierarchicalSymbols(List<HierarchicalSymbol> symbols, CancellationToken cancellationToken)
        {
            // Sort by the start of the symbol definition (they're probably sorted but we need to be
            // certain otherwise this algorithm won't work). We only need to sort the list once, and
            // since the implementation is recursive, it's easiest to use the stack to track that
            // this is the first call.
            symbols.Sort((x1, x2) => x1.Range.Start.CompareTo(x2.Range.Start));
            return SortHierarchicalSymbolsImpl(symbols, cancellationToken);
        }

        private static async Task SortHierarchicalSymbolsImpl(List<HierarchicalSymbol> symbols, CancellationToken cancellationToken)
        {
            for (int i = 0; i < symbols.Count; i++)
            {
                // This async method is pretty dense with synchronous code
                // so it's helpful to add some yields.
                await Task.Yield();
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                HierarchicalSymbol symbol = symbols[i];

                // Base case where we haven't found any parents yet (the first symbol must be a
                // parent by definition).
                if (i == 0)
                {
                    continue;
                }
                // If the symbol starts after end of last symbol parsed then it's a new parent.
                else if (symbol.Range.Start > symbols[i - 1].Range.End)
                {
                    continue;
                }
                // Otherwise it's a child, we just need to figure out whose child it is and move it there (which also means removing it from the current list).
                else
                {
                    for (int j = 0; j <= i; j++)
                    {
                        // While we should only check up to j < i, we iterate up to j <= i so that
                        // we can check this assertion that we didn't exhaust the parents.
                        Debug.Assert(j != i, "We didn't find the child's parent!");

                        HierarchicalSymbol parent = symbols[j];
                        // If the symbol starts after the parent starts and ends before the parent
                        // ends then its a child.
                        if (symbol.Range.Start > parent.Range.Start && symbol.Range.End < parent.Range.End)
                        {
                            // Add it to the parent's list.
                            parent.Children.Add(symbol);
                            // Remove it from this "parents" list (because it's a child) and adjust
                            // our loop counter because it's been removed.
                            symbols.RemoveAt(i);
                            i--;
                            break;
                        }
                    }
                }
            }

            // Now recursively sort the children into nested buckets of children too.
            foreach (HierarchicalSymbol parent in symbols)
            {
                // Since this modifies in place we just recurse, no re-assignment or clearing from
                // parent.Children necessary.
                await SortHierarchicalSymbols(parent.Children, cancellationToken).ConfigureAwait(false);
            }
        }

        // This struct and the mapping function below exist to allow us to skip a *bunch* of
        // unnecessary allocations when sorting the symbols since DocumentSymbol (which this is
        // pretty much a mirror of) is an immutable record...but we need to constantly modify the
        // list of children when sorting.
        private struct HierarchicalSymbol
        {
            public SymbolKind Kind;
            public Range Range;
            public Range SelectionRange;
            public string Name;
            public List<HierarchicalSymbol> Children;
        }

        // Recursively turn our HierarchicalSymbol struct into OmniSharp's DocumentSymbol record.
        private static List<DocumentSymbol> GetDocumentSymbolsFromHierarchicalSymbols(IEnumerable<HierarchicalSymbol> hierarchicalSymbols)
        {
            List<DocumentSymbol> documentSymbols = new();
            foreach (HierarchicalSymbol symbol in hierarchicalSymbols)
            {
                documentSymbols.Add(new DocumentSymbol
                {
                    Kind = symbol.Kind,
                    Range = symbol.Range,
                    SelectionRange = symbol.SelectionRange,
                    Name = symbol.Name,
                    Children = GetDocumentSymbolsFromHierarchicalSymbols(symbol.Children)
                });
            }
            return documentSymbols;
        }

        // AKA the outline feature!
        public override async Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handling document symbols for {request.TextDocument.Uri}");

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            List<HierarchicalSymbol> hierarchicalSymbols = new();

            foreach (SymbolReference symbolReference in ProvideDocumentSymbols(scriptFile))
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
                if (!symbolReference.IsDeclaration || symbolReference.Type is SymbolType.Parameter)
                {
                    continue;
                }

                hierarchicalSymbols.Add(new HierarchicalSymbol
                {
                    Kind = SymbolTypeUtils.GetSymbolKind(symbolReference.Type),
                    Range = symbolReference.ScriptRegion.ToRange(),
                    SelectionRange = symbolReference.NameRegion.ToRange(),
                    Name = symbolReference.Name,
                    Children = new List<HierarchicalSymbol>()
                });
            }

            // Short-circuit if we have no symbols.
            if (hierarchicalSymbols.Count == 0)
            {
                return s_emptySymbolInformationOrDocumentSymbolContainer;
            }

            // Otherwise slowly sort them into a hierarchy (this modifies the list).
            await SortHierarchicalSymbols(hierarchicalSymbols, cancellationToken).ConfigureAwait(false);

            // And finally convert them to the silly SymbolInformationOrDocumentSymbol wrapper.
            List<SymbolInformationOrDocumentSymbol> container = new();
            foreach (DocumentSymbol symbol in GetDocumentSymbolsFromHierarchicalSymbols(hierarchicalSymbols))
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
