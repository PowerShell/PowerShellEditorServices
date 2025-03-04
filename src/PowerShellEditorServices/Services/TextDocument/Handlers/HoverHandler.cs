// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    internal class PsesHoverHandler : HoverHandlerBase
    {
        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        public PsesHoverHandler(
            ILoggerFactory factory,
            SymbolsService symbolsService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesHoverHandler>();
            _symbolsService = symbolsService;
            _workspaceService = workspaceService;
        }

        protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        public override async Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Hover request canceled for file: {Uri}", request.TextDocument.Uri);
                return null;
            }

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            SymbolDetails symbolDetails =
                await _symbolsService.FindSymbolDetailsAtLocationAsync(
                        scriptFile,
                        request.Position.Line + 1,
                        request.Position.Character + 1,
                        cancellationToken).ConfigureAwait(false);

            if (symbolDetails is null)
            {
                return null;
            }

            return new Hover
            {
                Contents = new(symbolDetails.Documentation.MarkupContent),
                Range = symbolDetails.SymbolReference.NameRegion.ToRange()
            };
        }
    }
}
