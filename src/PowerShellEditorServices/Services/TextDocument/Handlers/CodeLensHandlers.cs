// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.CodeLenses;
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
        private static readonly CodeLensContainer s_emptyCodeLensContainer = new();
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
            _logger.LogDebug($"Handling code lens request for {request.TextDocument.Uri}");

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            IEnumerable<CodeLens> codeLensResults = ProvideCodeLenses(scriptFile);

            return cancellationToken.IsCancellationRequested
                ? Task.FromResult(s_emptyCodeLensContainer)
                : Task.FromResult(new CodeLensContainer(codeLensResults));
        }

        public override Task<CodeLens> Handle(CodeLens request, CancellationToken cancellationToken)
        {
            // TODO: Catch deserialization exception on bad object
            CodeLensData codeLensData = request.Data.ToObject<CodeLensData>();

            ICodeLensProvider originalProvider = _symbolsService
                .GetCodeLensProviders()
                .FirstOrDefault(provider => provider.ProviderId.Equals(codeLensData.ProviderId, StringComparison.Ordinal));

            ScriptFile scriptFile = _workspaceService.GetFile(codeLensData.Uri);
            return originalProvider.ResolveCodeLens(request, scriptFile, cancellationToken);
        }

        /// <summary>
        /// Get all the CodeLenses for a given script file.
        /// </summary>
        /// <param name="scriptFile">The PowerShell script file to get CodeLenses for.</param>
        /// <returns>All generated CodeLenses for the given script file.</returns>
        private IEnumerable<CodeLens> ProvideCodeLenses(ScriptFile scriptFile)
        {
            foreach (ICodeLensProvider provider in _symbolsService.GetCodeLensProviders())
            {
                foreach (CodeLens codeLens in provider.ProvideCodeLenses(scriptFile))
                {
                    yield return codeLens;
                }
            }
        }
    }
}
