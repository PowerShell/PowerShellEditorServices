//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesCodeLensHandlers : ICodeLensHandler, ICodeLensResolveHandler
    {
        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        private CodeLensCapability _capability;

        public PsesCodeLensHandlers(ILoggerFactory factory, SymbolsService symbolsService, WorkspaceService workspaceService, ConfigurationService configurationService)
        {
            _logger = factory.CreateLogger<PsesCodeLensHandlers>();
            _workspaceService = workspaceService;
            _symbolsService = symbolsService;
        }

        CodeLensRegistrationOptions IRegistration<CodeLensRegistrationOptions>.GetRegistrationOptions()
        {
            return new CodeLensRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector,
                ResolveProvider = true
            };
        }

        public Task<CodeLensContainer> Handle(CodeLensParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            CodeLens[] codeLensResults = ProvideCodeLenses(scriptFile);

            return Task.FromResult(new CodeLensContainer(codeLensResults));
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector,
            };
        }

        public bool CanResolve(CodeLens value)
        {
            CodeLensData codeLensData = value.Data.ToObject<CodeLensData>();
            return value?.Data != null && _symbolsService.GetCodeLensProviders().Any(provider => provider.ProviderId.Equals(codeLensData.ProviderId));
        }

        public Task<CodeLens> Handle(CodeLens request, CancellationToken cancellationToken)
        {
            // TODO: Catch deserializtion exception on bad object
            CodeLensData codeLensData = request.Data.ToObject<CodeLensData>();

            ICodeLensProvider originalProvider = _symbolsService
                .GetCodeLensProviders()
                .FirstOrDefault(provider => provider.ProviderId.Equals(codeLensData.ProviderId));

            ScriptFile scriptFile =
                _workspaceService.GetFile(
                    codeLensData.Uri);

            var resolvedCodeLens = originalProvider.ResolveCodeLens(request, scriptFile);
            return Task.FromResult(resolvedCodeLens);
        }

        public void SetCapability(CodeLensCapability capability)
        {
            _capability = capability;
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
        private IEnumerable<TResult> InvokeProviders<TResult>(
            Func<ICodeLensProvider, TResult> invokeFunc)
        {
            Stopwatch invokeTimer = new Stopwatch();
            List<TResult> providerResults = new List<TResult>();

            foreach (ICodeLensProvider provider in _symbolsService.GetCodeLensProviders())
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
    }
}
