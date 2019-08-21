using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    public class HoverHandler : IHoverHandler
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
        private readonly PowerShellContextService _powerShellContextService;

        private HoverCapability _capability;

        public HoverHandler(
            ILoggerFactory factory,
            SymbolsService symbolsService,
            WorkspaceService workspaceService,
            PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<HoverHandler>();
            _symbolsService = symbolsService;
            _workspaceService = workspaceService;
            _powerShellContextService = powerShellContextService;
        }

        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions
            {
                DocumentSelector = _documentSelector,
            };
        }

        public async Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile =
                _workspaceService.GetFile(
                    request.TextDocument.Uri.ToString());

            SymbolDetails symbolDetails =
                await _symbolsService.FindSymbolDetailsAtLocationAsync(
                        scriptFile,
                        (int) request.Position.Line + 1,
                        (int) request.Position.Character + 1);

            List<MarkedString> symbolInfo = new List<MarkedString>();
            Range symbolRange = null;

            if (symbolDetails != null)
            {
                symbolInfo.Add(new MarkedString("PowerShell", symbolDetails.DisplayString));

                if (!string.IsNullOrEmpty(symbolDetails.Documentation))
                {
                    symbolInfo.Add(new MarkedString("markdown", symbolDetails.Documentation));
                }

                symbolRange = GetRangeFromScriptRegion(symbolDetails.SymbolReference.ScriptRegion);
            }

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
