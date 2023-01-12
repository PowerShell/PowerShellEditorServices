// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesReferencesHandler : ReferencesHandlerBase
    {
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        public PsesReferencesHandler(SymbolsService symbolsService, WorkspaceService workspaceService)
        {
            _symbolsService = symbolsService;
            _workspaceService = workspaceService;
        }

        protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        public override async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            SymbolReference foundSymbol =
                SymbolsService.FindSymbolAtLocation(
                    scriptFile,
                    request.Position.Line + 1,
                    request.Position.Character + 1);

            IEnumerable<SymbolReference> referencesResult =
                await _symbolsService.ScanForReferencesOfSymbol(
                    foundSymbol,
                    _workspaceService.ExpandScriptReferences(scriptFile),
                    cancellationToken).ConfigureAwait(false);

            List<Location> locations = new();

            if (referencesResult != null)
            {
                foreach (SymbolReference foundReference in referencesResult)
                {
                    locations.Add(new Location
                    {
                        Uri = DocumentUri.From(foundReference.FilePath),
                        Range = foundReference.ScriptRegion.ToRange()
                    });
                }
            }

            return new LocationContainer(locations);
        }
    }
}
