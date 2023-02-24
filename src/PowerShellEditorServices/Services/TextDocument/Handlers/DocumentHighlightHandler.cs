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
    internal class PsesDocumentHighlightHandler : DocumentHighlightHandlerBase
    {
        private static readonly DocumentHighlightContainer s_emptyHighlightContainer = new();
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;

        public PsesDocumentHighlightHandler(
            ILoggerFactory loggerFactory,
            WorkspaceService workspaceService)
        {
            _logger = loggerFactory.CreateLogger<PsesDocumentHighlightHandler>();
            _workspaceService = workspaceService;
        }

        protected override DocumentHighlightRegistrationOptions CreateRegistrationOptions(DocumentHighlightCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        public override Task<DocumentHighlightContainer> Handle(
            DocumentHighlightParams request,
            CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            IEnumerable<SymbolReference> occurrences = SymbolsService.FindOccurrencesInFile(
                scriptFile,
                request.Position.Line + 1,
                request.Position.Character + 1);

            if (occurrences is null)
            {
                return Task.FromResult(s_emptyHighlightContainer);
            }

            List<DocumentHighlight> highlights = new();
            foreach (SymbolReference occurrence in occurrences)
            {
                highlights.Add(new DocumentHighlight
                {
                    Kind = DocumentHighlightKind.Write, // TODO: Which symbol types are writable?
                    Range = occurrence.NameRegion.ToRange() // Just the symbol name
                });
            }

            _logger.LogDebug("Highlights: " + highlights);

            return Task.FromResult(new DocumentHighlightContainer(highlights));
        }
    }
}
