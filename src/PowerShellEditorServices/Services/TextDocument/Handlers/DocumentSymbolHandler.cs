// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesDocumentSymbolHandler : DocumentSymbolHandlerBase
    {
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
                new PesterDocumentSymbolProvider()
            };
        }

        protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        // AKA the outline feature
        public override async Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            IEnumerable<SymbolReference> foundSymbols = ProvideDocumentSymbols(scriptFile);
            if (foundSymbols is null)
            {
                return null;
            }

            string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

            List<SymbolInformationOrDocumentSymbol> symbols = new();
            foreach (SymbolReference r in foundSymbols)
            {
                // This async method is pretty dense with synchronous code
                // so it's helpful to add some yields.
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();

                // Outline view should only include declarations.
                //
                // TODO: We should also include function invocations that are part of DSLs (like
                // Invoke-Build etc.).
                if (!r.IsDeclaration || r.SymbolType is SymbolType.Parameter)
                {
                    continue;
                }

                // TODO: This should be a DocumentSymbol now as SymbolInformation is deprecated. But
                // this requires figuring out how to populate `children`. Once we do that, the range
                // can be NameRegion.
                //
                // symbols.Add(new SymbolInformationOrDocumentSymbol(new DocumentSymbol
                // {
                //     Name = SymbolTypeUtils.GetDecoratedSymbolName(r),
                //     Kind = SymbolTypeUtils.GetSymbolKind(r.SymbolType),
                //     Range = r.ScriptRegion.ToRange(),
                //     SelectionRange = r.NameRegion.ToRange()
                // }));
                symbols.Add(new SymbolInformationOrDocumentSymbol(new SymbolInformation
                {
                    ContainerName = containerName,
                    Kind = SymbolTypeUtils.GetSymbolKind(r.SymbolType),
                    Location = new Location
                    {
                        Uri = DocumentUri.From(r.FilePath),
                        Range = r.ScriptRegion.ToRange() // The whole thing, not just the name.
                    },
                    Name = r.DisplayString
                }));
            }

            return new SymbolInformationOrDocumentSymbolContainer(symbols);
        }

        private IEnumerable<SymbolReference> ProvideDocumentSymbols(ScriptFile scriptFile)
        {
            return InvokeProviders(p => p.ProvideDocumentSymbols(scriptFile))
                    .SelectMany(r => r);
        }

        /// <summary>
        /// Invokes the given function synchronously against all
        /// registered providers.
        /// </summary>
        /// <param name="invokeFunc">The function to be invoked.</param>
        /// <returns>
        /// An IEnumerable containing the results of all providers
        /// that were invoked successfully.
        /// </returns>
        protected IEnumerable<TResult> InvokeProviders<TResult>(
            Func<IDocumentSymbolProvider, TResult> invokeFunc)
        {
            Stopwatch invokeTimer = new();
            List<TResult> providerResults = new();

            foreach (IDocumentSymbolProvider provider in _providers)
            {
                try
                {
                    invokeTimer.Restart();

                    providerResults.Add(invokeFunc(provider));

                    invokeTimer.Stop();

                    _logger.LogTrace(
                        $"Invocation of provider '{provider.GetType().Name}' completed in {invokeTimer.ElapsedMilliseconds}ms.");
                }
                catch (Exception e)
                {
                    _logger.LogException(
                        $"Exception caught while invoking provider {provider.GetType().Name}:",
                        e);
                }
            }

            return providerResults;
        }
    }
}
