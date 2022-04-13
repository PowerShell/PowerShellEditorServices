﻿// Copyright (c) Microsoft Corporation.
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
    internal class PsesDefinitionHandler : DefinitionHandlerBase
    {
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        public PsesDefinitionHandler(
            SymbolsService symbolsService,
            WorkspaceService workspaceService)
        {
            _symbolsService = symbolsService;
            _workspaceService = workspaceService;
        }

        protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        public override async Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            SymbolReference foundSymbol =
                SymbolsService.FindSymbolAtLocation(
                    scriptFile,
                    request.Position.Line + 1,
                    request.Position.Character + 1);

            List<LocationOrLocationLink> definitionLocations = new();
            if (foundSymbol != null)
            {
                SymbolReference foundDefinition = await _symbolsService.GetDefinitionOfSymbolAsync(
                        scriptFile,
                        foundSymbol).ConfigureAwait(false);

                if (foundDefinition != null)
                {
                    definitionLocations.Add(
                        new LocationOrLocationLink(
                            new Location
                            {
                                Uri = DocumentUri.From(foundDefinition.FilePath),
                                Range = GetRangeFromScriptRegion(foundDefinition.ScriptRegion)
                            }));
                }
            }

            return new LocationOrLocationLinks(definitionLocations);
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
