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

        // This turns a flat list of symbols into a hierarchical list.
        private static async Task<List<HierarchicalSymbol>> SortHierarchicalSymbols(List<HierarchicalSymbol> symbols, CancellationToken cancellationToken)
        {
            // Sort by the start of the symbol definition (they're probably sorted but we need to be
            // certain otherwise this algorithm won't work).
            symbols.Sort((x1, x2) => x1.Range.Start.CompareTo(x2.Range.Start));

            List<HierarchicalSymbol> parents = new();

            foreach (HierarchicalSymbol symbol in symbols)
            {
                // This async method is pretty dense with synchronous code
                // so it's helpful to add some yields.
                await Task.Yield();
                if (cancellationToken.IsCancellationRequested)
                {
                    return parents;
                }
                // Base case where we haven't found any parents yet.
                if (parents.Count == 0)
                {
                    parents.Add(symbol);
                }
                // If the symbol starts after end of last symbol parsed then it's a new parent.
                else if (symbol.Range.Start > parents[parents.Count - 1].Range.End)
                {
                    parents.Add(symbol);
                }
                // Otherwise it's a child, we just need to figure out whose child it is.
                else
                {
                    foreach (HierarchicalSymbol parent in parents)
                    {
                        // If the symbol starts after the parent starts and ends before the parent
                        // ends then its a child.
                        if (symbol.Range.Start > parent.Range.Start && symbol.Range.End < parent.Range.End)
                        {
                            parent.Children.Add(symbol);
                            break;
                        }
                    }
                    // TODO: If we somehow exist the list of parents and didn't find a place for the
                    // child...we have a problem.
                }
            }

            // Now recursively sort the children into nested buckets of children too.
            foreach (HierarchicalSymbol parent in parents)
            {
                List<HierarchicalSymbol> sortedChildren = await SortHierarchicalSymbols(parent.Children, cancellationToken).ConfigureAwait(false);
                // Since this is a foreach we can't just assign to parent.Children and have to do
                // this instead.
                parent.Children.Clear();
                parent.Children.AddRange(sortedChildren);
            }

            return parents;
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

            // Otherwise slowly sort them into a hierarchy.
            hierarchicalSymbols = await SortHierarchicalSymbols(hierarchicalSymbols, cancellationToken).ConfigureAwait(false);

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
