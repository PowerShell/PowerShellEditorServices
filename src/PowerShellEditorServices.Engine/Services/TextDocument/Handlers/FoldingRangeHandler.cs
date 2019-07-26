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
    public class FoldingRangeHandler : IFoldingRangeHandler
    {
        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.ps*1"
            }
        );

        private readonly ILogger _logger;
        private readonly ConfigurationService _configurationService;
        private readonly WorkspaceService _workspaceService;

        private FoldingRangeCapability _capability;

        public FoldingRangeHandler(ILoggerFactory factory, ConfigurationService configurationService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<FoldingRangeHandler>();
            _configurationService = configurationService;
            _workspaceService = workspaceService;
        }
        public TextDocumentRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
            };
        }

        public Task<Container<FoldingRange>> Handle(FoldingRangeRequestParam request, CancellationToken cancellationToken)
        {
            // TODO Should be using dynamic registrations
            if (!_configurationService.CurrentSettings.CodeFolding.Enable) { return null; }

            // Avoid crash when using untitled: scheme or any other scheme where the document doesn't
            // have a backing file.  https://github.com/PowerShell/vscode-powershell/issues/1676
            // Perhaps a better option would be to parse the contents of the document as a string
            // as opposed to reading a file but the scenario of "no backing file" probably doesn't
            // warrant the extra effort.
            if (!_workspaceService.TryGetFile(request.TextDocument.Uri.ToString(), out ScriptFile scriptFile)) { return null; }

            var result = new List<FoldingRange>();

            // If we're showing the last line, decrement the Endline of all regions by one.
            int endLineOffset = _configurationService.CurrentSettings.CodeFolding.ShowLastLine ? -1 : 0;

            foreach (FoldingReference fold in TokenOperations.FoldableReferences(scriptFile.ScriptTokens).References)
            {
                result.Add(new FoldingRange {
                    EndCharacter   = fold.EndCharacter,
                    EndLine        = fold.EndLine + endLineOffset,
                    Kind           = fold.Kind,
                    StartCharacter = fold.StartCharacter,
                    StartLine      = fold.StartLine
                });
            }

            return Task.FromResult(new Container<FoldingRange>(result));
        }

        public void SetCapability(FoldingRangeCapability capability)
        {
            _capability = capability;
        }
    }
}
