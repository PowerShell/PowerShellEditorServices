//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;
using Microsoft.PowerShell.EditorServices.Symbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using PowerShellEditorServices.Engine.Utility;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    public class DefinitionHandler : IDefinitionHandler
    {
        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter
            {
                Language = "powershell"
            }
        );

        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        private DefinitionCapability _capability;

        public DefinitionHandler(
            ILoggerFactory factory,
            SymbolsService symbolsService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<DefinitionHandler>();
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

        public async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile =
                _workspaceService.GetFile(
                    request.TextDocument.Uri.ToString());

            SymbolReference foundSymbol =
                _symbolsService.FindSymbolAtLocation(
                    scriptFile,
                    (int) request.Position.Line + 1,
                    (int) request.Position.Character + 1);

            List<LocationOrLocationLink> definitionLocations = new List<LocationOrLocationLink>();
            if (foundSymbol != null)
            {
                SymbolReference foundDefinition = await _symbolsService.GetDefinitionOfSymbolAsync(
                        scriptFile,
                        foundSymbol);

                if (foundDefinition != null)
                {
                    definitionLocations.Add(
                        new LocationOrLocationLink(
                            new Location
                            {
                                Uri = PathUtils.ToUri(foundDefinition.FilePath),
                                Range = GetRangeFromScriptRegion(foundDefinition.ScriptRegion)
                            }));
                }
            }

            return new LocationOrLocationLinks(definitionLocations);
        }

        public void SetCapability(DefinitionCapability capability)
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
