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

        public override Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            IEnumerable<ISymbolReference> foundSymbols =
                ProvideDocumentSymbols(scriptFile);

            SymbolInformationOrDocumentSymbol[] symbols = null;

            string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

            symbols = foundSymbols != null
                ? foundSymbols
                        .Select(r =>
                        {
                            return new SymbolInformationOrDocumentSymbol(new SymbolInformation
                            {
                                ContainerName = containerName,
                                Kind = GetSymbolKind(r.SymbolType),
                                Location = new Location
                                {
                                    Uri = DocumentUri.From(r.FilePath),
                                    Range = GetRangeFromScriptRegion(r.ScriptRegion)
                                },
                                Name = GetDecoratedSymbolName(r)
                            });
                        })
                        .ToArray()
                : Array.Empty<SymbolInformationOrDocumentSymbol>();

            return Task.FromResult(new SymbolInformationOrDocumentSymbolContainer(symbols));
        }

        private IEnumerable<ISymbolReference> ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            return
                InvokeProviders(p => p.ProvideDocumentSymbols(scriptFile))
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

        private static SymbolKind GetSymbolKind(SymbolType symbolType)
        {
            return symbolType switch
            {
                SymbolType.Configuration or SymbolType.Function or SymbolType.Workflow => SymbolKind.Function,
                _ => SymbolKind.Variable,
            };
        }

        private static string GetDecoratedSymbolName(ISymbolReference symbolReference)
        {
            string name = symbolReference.SymbolName;

            if (symbolReference.SymbolType is SymbolType.Configuration or
                SymbolType.Function or
                SymbolType.Workflow)
            {
                name += " { }";
            }

            return name;
        }

        private static Range GetRangeFromScriptRegion(ScriptRegion scriptRegion)
        {
            return new Range
            {
                Start = new Position
                {
                    Line = scriptRegion.StartLineNumber - 1,
                    Character = scriptRegion.StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = scriptRegion.EndLineNumber - 1,
                    Character = scriptRegion.EndColumnNumber - 1
                }
            };
        }
    }
}
