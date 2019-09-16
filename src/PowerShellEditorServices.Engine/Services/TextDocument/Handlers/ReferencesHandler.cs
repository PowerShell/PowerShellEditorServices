//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Engine.Services;
using Microsoft.PowerShell.EditorServices.Engine.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Engine.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Engine.Handlers
{
    class ReferencesHandler : IReferencesHandler
    {
        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Language = "powershell"
            }
        );

        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;
        private ReferencesCapability _capability;

        public ReferencesHandler(ILoggerFactory factory, SymbolsService symbolsService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<ReferencesHandler>();
            _symbolsService = symbolsService;
            _workspaceService = workspaceService;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions
            {
                DocumentSelector = _documentSelector
            };
        }

        public async Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile =
                _workspaceService.GetFile(
                    request.TextDocument.Uri.ToString());

            SymbolReference foundSymbol =
                _symbolsService.FindSymbolAtLocation(
                    scriptFile,
                    (int)request.Position.Line + 1,
                    (int)request.Position.Character + 1);

            List<SymbolReference> referencesResult =
                _symbolsService.FindReferencesOfSymbol(
                    foundSymbol,
                    _workspaceService.ExpandScriptReferences(scriptFile),
                    _workspaceService);

            var locations = new List<Location>();

            if (referencesResult != null)
            {
                foreach (SymbolReference foundReference in referencesResult)
                {
                    locations.Add(new Location
                    {
                        Uri = PathUtils.ToUri(foundReference.FilePath),
                        Range = GetRangeFromScriptRegion(foundReference.ScriptRegion)
                    });
                }
            }

            return new LocationContainer(locations);
        }

        public void SetCapability(ReferencesCapability capability)
        {
            _capability = capability;
        }

        private static Range GetRangeFromScriptRegion(ScriptRegion scriptRegion)
        {
            return new Range
            {
                Start = new Position
                {
                    Line = scriptRegion.StartLineNumber - 1,
                    Character = scriptRegion.StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = scriptRegion.EndLineNumber - 1,
                    Character = scriptRegion.EndColumnNumber - 1
                }
            };
        }
    }
}
