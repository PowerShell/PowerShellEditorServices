// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesWorkspaceSymbolsHandler : WorkspaceSymbolsHandlerBase
    {
        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        public PsesWorkspaceSymbolsHandler(ILoggerFactory loggerFactory, SymbolsService symbols, WorkspaceService workspace)
        {
            _logger = loggerFactory.CreateLogger<PsesWorkspaceSymbolsHandler>();
            _symbolsService = symbols;
            _workspaceService = workspace;
        }

        protected override WorkspaceSymbolRegistrationOptions CreateRegistrationOptions(WorkspaceSymbolCapability capability, ClientCapabilities clientCapabilities) => new() { };

        public override Task<Container<SymbolInformation>> Handle(WorkspaceSymbolParams request, CancellationToken cancellationToken)
        {
            List<SymbolInformation> symbols = new();

            foreach (ScriptFile scriptFile in _workspaceService.GetOpenedFiles())
            {
                List<SymbolReference> foundSymbols =
                    _symbolsService.FindSymbolsInFile(
                        scriptFile);

                // TODO: Need to compute a relative path that is based on common path for all workspace files
                string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

                foreach (SymbolReference foundOccurrence in foundSymbols)
                {
                    if (!IsQueryMatch(request.Query, foundOccurrence.SymbolName))
                    {
                        continue;
                    }

                    Location location = new()
                    {
                        Uri = DocumentUri.From(foundOccurrence.FilePath),
                        Range = GetRangeFromScriptRegion(foundOccurrence.ScriptRegion)
                    };

                    symbols.Add(new SymbolInformation
                    {
                        ContainerName = containerName,
                        Kind = foundOccurrence.SymbolType == SymbolType.Variable ? SymbolKind.Variable : SymbolKind.Function,
                        Location = location,
                        Name = GetDecoratedSymbolName(foundOccurrence)
                    });
                }
            }
            _logger.LogWarning("Logging in a handler works now.");

            return Task.FromResult(new Container<SymbolInformation>(symbols));
        }

        #region private Methods

        private static bool IsQueryMatch(string query, string symbolName) => symbolName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

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

        private static string GetDecoratedSymbolName(SymbolReference symbolReference)
        {
            string name = symbolReference.SymbolName;

            if (symbolReference.SymbolType is SymbolType.Configuration or
                SymbolType.Function or
                SymbolType.Workflow)
            {
                name += " { }";
            }

            return name;
        }

        #endregion
    }
}
