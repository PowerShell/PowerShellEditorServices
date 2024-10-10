// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Microsoft.PowerShell.EditorServices.Handlers;

/// <summary>
/// Resolves PowerShell types and parameters as inlay hints for the LSP client. See: https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_inlayHints
/// </summary>
internal class PsesInlayHandler(
    ILoggerFactory loggerFactory,
    SymbolsService symbolsService,
    WorkspaceService workspaceService
) : InlayHintsHandlerBase
{
    private readonly ILogger logger = loggerFactory.CreateLogger<PsesInlayHandler>();

    /// <summary>
    /// Expresses the capabilities of our Inlay Hints handler to the LSP
    /// </summary>
    protected override InlayHintRegistrationOptions CreateRegistrationOptions(InlayHintClientCapabilities capability, ClientCapabilities clientCapabilities) => new()
    {
        DocumentSelector = LspUtils.PowerShellDocumentSelector,
        WorkDoneProgress = false, //TODO: Report progress for large documents
        ResolveProvider = false //TODO: Add a resolve Provider for detailed inlay information
    };

    public override async Task<InlayHint> Handle(InlayHint request, CancellationToken cancellationToken) => throw new NotImplementedException();

    public override async Task<InlayHintContainer> Handle(InlayHintParams request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("InlayHint request canceled for file: {Uri}", request.TextDocument.Uri);
            return null;
        }

        // TODO: Limit search to request.range
        ScriptFile scriptFile = workspaceService.GetFile(request.TextDocument.Uri);

        IEnumerable<SymbolReference> symbolReferences =
            symbolsService.FindSymbolsInFile(scriptFile);

        if (symbolReferences is null)
        {
            return null;
        }

        IEnumerable<InlayHint> inlayHints =
            from s in symbolReferences
            where s.Type == SymbolType.Variable | s.Type == SymbolType.Parameter
            select new InlayHint
            {
                Kind = InlayHintKind.Type,
                Position = new Position(
                    s.ScriptRegion.StartLineNumber - 1,
                    s.ScriptRegion.StartColumnNumber - 1),
                Label = "TypeGoesHere:" //Fixme: Get the type of the symbol
            };

        return new InlayHintContainer(inlayHints);
    }
}
