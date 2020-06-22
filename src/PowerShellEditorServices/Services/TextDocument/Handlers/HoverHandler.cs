//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
    internal class PsesHoverHandler : IHoverHandler
    {
        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;

        private HoverCapability _capability;

        public PsesHoverHandler(
            ILoggerFactory factory,
            SymbolsService symbolsService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesHoverHandler>();
            _symbolsService = symbolsService;
            _workspaceService = workspaceService;
        }

        public HoverRegistrationOptions GetRegistrationOptions()
        {
            return new HoverRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector
            };
        }

        public async Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Hover request canceled for file: {0}", request.TextDocument.Uri);
                return null;
            }

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            SymbolDetails symbolDetails =
                await _symbolsService.FindSymbolDetailsAtLocationAsync(
                        scriptFile,
                        request.Position.Line + 1,
                        request.Position.Character + 1).ConfigureAwait(false);

            if (symbolDetails == null)
            {
                return null;
            }

            List<MarkedString> symbolInfo = new List<MarkedString>();
            symbolInfo.Add(new MarkedString("PowerShell", symbolDetails.DisplayString));

            if (!string.IsNullOrEmpty(symbolDetails.Documentation))
            {
                symbolInfo.Add(new MarkedString("markdown", symbolDetails.Documentation));
            }

            Range symbolRange = GetRangeFromScriptRegion(symbolDetails.SymbolReference.ScriptRegion);

            return new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(symbolInfo),
                Range = symbolRange
            };
        }

        public void SetCapability(HoverCapability capability)
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
