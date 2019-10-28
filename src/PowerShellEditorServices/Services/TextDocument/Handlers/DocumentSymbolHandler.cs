//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    public class DocumentSymbolHandler : IDocumentSymbolHandler
    {
        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter
            {
                Language = "powershell"
            }
        );

        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        private readonly IDocumentSymbolProvider[] _providers;

        private DocumentSymbolCapability _capability;

        public DocumentSymbolHandler(ILoggerFactory factory, ConfigurationService configurationService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<FoldingRangeHandler>();
            _workspaceService = workspaceService;
            _providers = new IDocumentSymbolProvider[]
            {
                new ScriptDocumentSymbolProvider(),
                new PsdDocumentSymbolProvider(),
                new PesterDocumentSymbolProvider()
            };
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions
            {
                DocumentSelector = _documentSelector,
            };
        }

        public Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile =
                _workspaceService.GetFile(
                    request.TextDocument.Uri.ToString());

            IEnumerable<SymbolReference> foundSymbols =
                this.ProvideDocumentSymbols(scriptFile);

            SymbolInformationOrDocumentSymbol[] symbols = null;

            string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

            if (foundSymbols != null)
            {
                symbols =
                    foundSymbols
                        .Select(r =>
                        {
                            return new SymbolInformationOrDocumentSymbol(new SymbolInformation
                            {
                                ContainerName = containerName,
                                Kind = GetSymbolKind(r.SymbolType),
                                Location = new Location
                                {
                                    Uri = PathUtils.ToUri(r.FilePath),
                                    Range = GetRangeFromScriptRegion(r.ScriptRegion)
                                },
                                Name = GetDecoratedSymbolName(r)
                            });
                        })
                        .ToArray();
            }
            else
            {
                symbols = new SymbolInformationOrDocumentSymbol[0];
            }


            return Task.FromResult(new SymbolInformationOrDocumentSymbolContainer(symbols));
        }

        public void SetCapability(DocumentSymbolCapability capability)
        {
            _capability = capability;
        }

        private IEnumerable<SymbolReference> ProvideDocumentSymbols(
            ScriptFile scriptFile)
        {
            return
                this.InvokeProviders(p => p.ProvideDocumentSymbols(scriptFile))
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
            Stopwatch invokeTimer = new Stopwatch();
            List<TResult> providerResults = new List<TResult>();

            foreach (var provider in this._providers)
            {
                try
                {
                    invokeTimer.Restart();

                    providerResults.Add(invokeFunc(provider));

                    invokeTimer.Stop();

                    this._logger.LogTrace(
                        $"Invocation of provider '{provider.GetType().Name}' completed in {invokeTimer.ElapsedMilliseconds}ms.");
                }
                catch (Exception e)
                {
                    this._logger.LogException(
                        $"Exception caught while invoking provider {provider.GetType().Name}:",
                        e);
                }
            }

            return providerResults;
        }

        private static SymbolKind GetSymbolKind(SymbolType symbolType)
        {
            switch (symbolType)
            {
                case SymbolType.Configuration:
                case SymbolType.Function:
                case SymbolType.Workflow:
                    return SymbolKind.Function;

                default:
                    return SymbolKind.Variable;
            }
        }

        private static string GetDecoratedSymbolName(SymbolReference symbolReference)
        {
            string name = symbolReference.SymbolName;

            if (symbolReference.SymbolType == SymbolType.Configuration ||
                symbolReference.SymbolType == SymbolType.Function ||
                symbolReference.SymbolType == SymbolType.Workflow)
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
