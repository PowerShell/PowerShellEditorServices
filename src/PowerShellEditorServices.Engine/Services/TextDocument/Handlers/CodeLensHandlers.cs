using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;
using Microsoft.PowerShell.EditorServices.CodeLenses;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    public class CodeLensHandlers : ICodeLensHandler, ICodeLensResolveHandler
    {
        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.ps*1"
            }
        );

        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        private readonly ICodeLensProvider[] _providers;

        private CodeLensCapability _capability;

        public CodeLensHandlers(ILoggerFactory factory, SymbolsService symbolsService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<FoldingRangeHandler>();
            _workspaceService = workspaceService;
            _symbolsService = symbolsService;
            _providers = new ICodeLensProvider[]
            {
                new ReferencesCodeLensProvider(_workspaceService, _symbolsService),
                new PesterCodeLensProvider()
            };
        }

        CodeLensRegistrationOptions IRegistration<CodeLensRegistrationOptions>.GetRegistrationOptions()
        {
            return new CodeLensRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                ResolveProvider = true
            };
        }

        public Task<CodeLensContainer> Handle(CodeLensParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(
                request.TextDocument.Uri.ToString());

            CodeLens[] codeLensResults = ProvideCodeLenses(scriptFile);

            return Task.FromResult(new CodeLensContainer(codeLensResults));
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        public bool CanResolve(CodeLens value)
        {
            CodeLensData codeLensData = value.Data.ToObject<CodeLensData>();
            return value?.Data != null && _providers.Any(provider => provider.ProviderId.Equals(codeLensData.ProviderId));
        }

        public Task<CodeLens> Handle(CodeLens request, CancellationToken cancellationToken)
        {
            // TODO: Catch deserializtion exception on bad object
            CodeLensData codeLensData = request.Data.ToObject<CodeLensData>();

            ICodeLensProvider originalProvider =
                _providers.FirstOrDefault(
                    provider => provider.ProviderId.Equals(codeLensData.ProviderId));

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
    }
}
