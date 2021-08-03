// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesDocumentHighlightHandler : DocumentHighlightHandlerBase
    {
        private static readonly DocumentHighlightContainer s_emptyHighlightContainer = new DocumentHighlightContainer();
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;
        private readonly SymbolsService _symbolsService;

        public PsesDocumentHighlightHandler(
            ILoggerFactory loggerFactory,
            WorkspaceService workspaceService,
            SymbolsService symbolService)
        {
            _logger = loggerFactory.CreateLogger<PsesDocumentHighlightHandler>();
            _workspaceService = workspaceService;
            _symbolsService = symbolService;
            _logger.LogInformation("highlight handler loaded");
        }

        protected override DocumentHighlightRegistrationOptions CreateRegistrationOptions(DocumentHighlightCapability capability, ClientCapabilities clientCapabilities) => new DocumentHighlightRegistrationOptions
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        public override Task<DocumentHighlightContainer> Handle(
            DocumentHighlightParams request,
            CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            IReadOnlyList<SymbolReference> symbolOccurrences = SymbolsService.FindOccurrencesInFile(
                scriptFile,
                request.Position.Line + 1,
                request.Position.Character + 1);

            if (symbolOccurrences == null)
            {
                return Task.FromResult(s_emptyHighlightContainer);
            }

            var highlights = new DocumentHighlight[symbolOccurrences.Count];
            for (int i = 0; i < symbolOccurrences.Count; i++)
            {
                highlights[i] = new DocumentHighlight
                {
                    Kind = DocumentHighlightKind.Write, // TODO: Which symbol types are writable?
                    Range = symbolOccurrences[i].ScriptRegion.ToRange()
                };
            }

            return Task.FromResult(new DocumentHighlightContainer(highlights));
        }
    }
}
