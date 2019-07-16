using System;
using System.Collections.Generic;
using System.IO;
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
    public class WorkspaceSymbolsHandler : IWorkspaceSymbolsHandler
    {
        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;
        private WorkspaceSymbolCapability _capability;

        public WorkspaceSymbolsHandler(ILoggerFactory loggerFactory, SymbolsService symbols, WorkspaceService workspace) {
            _logger = loggerFactory.CreateLogger<WorkspaceSymbolsHandler>();
            _symbolsService = symbols;
            _workspaceService = workspace;
        }

        public object GetRegistrationOptions()
        {
            return null;
            // throw new NotImplementedException();
        }

        public Task<SymbolInformationContainer> Handle(WorkspaceSymbolParams request, CancellationToken cancellationToken)
        {
            var symbols = new List<SymbolInformation>();

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

                    var location = new Location
                    {
                        Uri = PathUtils.ToUri(foundOccurrence.FilePath),
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

            return Task.FromResult(new SymbolInformationContainer(symbols));
        }

        public void SetCapability(WorkspaceSymbolCapability capability)
        {
            _capability = capability;
        }

        #region private Methods

        private bool IsQueryMatch(string query, string symbolName)
        {
            return symbolName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetFileUri(string filePath)
        {
            // If the file isn't untitled, return a URI-style path
            return
                !filePath.StartsWith("untitled") && !filePath.StartsWith("inmemory")
                    ? new Uri("file://" + filePath).AbsoluteUri
                    : filePath;
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

        private static string GetDecoratedSymbolName(SymbolReference symbolReference)
        {
            string name = symbolReference.SymbolName;

            if (symbolReference.SymbolType == SymbolType.Configuration ||
                symbolReference.SymbolType == SymbolType.Function ||
                symbolReference.SymbolType == SymbolType.Workflow)
            {
                name += " { }";
            }

            return name;
        }

        #endregion
    }
}
