//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using PowerShellEditorServices.Engine.Utility;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerShell.EditorServices.TextDocument
{
    public class PsesDocumentHighlightHandler : DocumentHighlightHandler
    {
        private static readonly DocumentHighlightContainer s_emptyHighlightContainer = new DocumentHighlightContainer();

        private readonly ILogger _logger;

        private readonly WorkspaceService _workspaceService;

        private readonly SymbolsService _symbolsService;

        public PsesDocumentHighlightHandler(
            ILoggerFactory loggerFactory,
            WorkspaceService workspaceService,
            SymbolsService symbolService,
            TextDocumentRegistrationOptions registrationOptions)
            : base(registrationOptions)
        {
            _logger = loggerFactory.CreateLogger<DocumentHighlightHandler>();
            _workspaceService = workspaceService;
            _symbolsService = symbolService;
        }

        public override Task<DocumentHighlightContainer> Handle(
            DocumentHighlightParams request,
            CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(PathUtils.FromUri(request.TextDocument.Uri));

            IReadOnlyList<SymbolReference> symbolOccurrences = _symbolsService.FindOccurrencesInFile(
                scriptFile,
                (int)(request.Position.Line + 1),
                (int)(request.Position.Character + 1));

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
