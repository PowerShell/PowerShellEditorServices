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
        private static readonly Container<SymbolInformation> s_emptySymbolInformationContainer = new();
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

        public override async Task<Container<SymbolInformation>> Handle(WorkspaceSymbolParams request, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Handling workspace symbols request for query {request.Query}");

            await _symbolsService.ScanWorkspacePSFiles(cancellationToken).ConfigureAwait(false);
            List<SymbolInformation> symbols = new();

            foreach (ScriptFile scriptFile in _workspaceService.GetOpenedFiles())
            {
                _logger.LogDebug($"Handling workspace symbols request for: {request.Query}");
                // TODO: Need to compute a relative path that is based on common path for all workspace files
                string containerName = Path.GetFileNameWithoutExtension(scriptFile.FilePath);

                foreach (SymbolReference symbol in _symbolsService.FindSymbolsInFile(scriptFile))
                {
                    // This async method is pretty dense with synchronous code
                    // so it's helpful to add some yields.
                    await Task.Yield();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (!symbol.IsDeclaration)
                    {
                        continue;
                    }

                    if (symbol.Type is SymbolType.Parameter)
                    {
                        continue;
                    }

                    if (!IsQueryMatch(request.Query, symbol.Name))
                    {
                        continue;
                    }

                    // Exclude Pester setup/teardown symbols as they're unnamed
                    if (symbol is PesterSymbolReference pesterSymbol &&
                        !PesterSymbolReference.IsPesterTestCommand(pesterSymbol.Command))
                    {
                        continue;
                    }

                    Location location = new()
                    {
                        Uri = DocumentUri.From(symbol.FilePath),
                        Range = symbol.NameRegion.ToRange()
                    };

                    // TODO: This should be a WorkplaceSymbol now as SymbolInformation is deprecated.
                    symbols.Add(new SymbolInformation
                    {
                        ContainerName = containerName,
                        Kind = SymbolTypeUtils.GetSymbolKind(symbol.Type),
                        Location = location,
                        Name = symbol.Name
                    });
                }
            }

            return symbols.Count == 0
                ? s_emptySymbolInformationContainer
                : symbols;
        }

        private static bool IsQueryMatch(string query, string symbolName) => symbolName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
