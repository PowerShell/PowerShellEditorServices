// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.CodeLenses;
using Microsoft.PowerShell.EditorServices.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesCodeLensHandlers : CodeLensHandlerBase
    {
        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        public PsesCodeLensHandlers(ILoggerFactory factory, SymbolsService symbolsService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesCodeLensHandlers>();
            _workspaceService = workspaceService;
            _symbolsService = symbolsService;
        }

        protected override CodeLensRegistrationOptions CreateRegistrationOptions(CodeLensCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector,
            ResolveProvider = true
        };

        public override Task<CodeLensContainer> Handle(CodeLensParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            CodeLens[] codeLensResults = ProvideCodeLenses(scriptFile);
            return Task.FromResult(new CodeLensContainer(codeLensResults));
        }

        public override Task<CodeLens> Handle(CodeLens request, CancellationToken cancellationToken)
        {
            // TODO: Catch deserializtion exception on bad object
            CodeLensData codeLensData = request.Data.ToObject<CodeLensData>();

            ICodeLensProvider originalProvider = _symbolsService
                .GetCodeLensProviders()
                .FirstOrDefault(provider => provider.ProviderId.Equals(codeLensData.ProviderId, StringComparison.Ordinal));

            ScriptFile scriptFile = _workspaceService.GetFile(codeLensData.Uri);

            return originalProvider.ResolveCodeLens(request, scriptFile);
        }

        /// <summary>
        /// Get all the CodeLenses for a given script file.
        /// </summary>
        /// <param name="scriptFile">The PowerShell script file to get CodeLenses for.</param>
        /// <returns>All generated CodeLenses for the given script file.</returns>
        private CodeLens[] ProvideCodeLenses(ScriptFile scriptFile)
        {
            return InvokeProviders(provider => provider.ProvideCodeLenses(scriptFile))
                .SelectMany(codeLens => codeLens)
                .ToArray();
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
        private IEnumerable<TResult> InvokeProviders<TResult>(Func<ICodeLensProvider, TResult> invokeFunc)
        {
            Stopwatch invokeTimer = new();
            List<TResult> providerResults = new();

            foreach (ICodeLensProvider provider in _symbolsService.GetCodeLensProviders())
            {
                try
                {
                    invokeTimer.Restart();
                    providerResults.Add(invokeFunc(provider));
                    invokeTimer.Stop();
                    _logger.LogTrace($"Invocation of provider '{provider.GetType().Name}' completed in {invokeTimer.ElapsedMilliseconds}ms.");
                }
                catch (Exception e)
                {
                    _logger.LogException($"Exception caught while invoking provider {provider.GetType().Name}:", e);
                }
            }

            return providerResults;
        }
    }
}
