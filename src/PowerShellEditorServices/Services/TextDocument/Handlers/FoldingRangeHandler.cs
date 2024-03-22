// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesFoldingRangeHandler : FoldingRangeHandlerBase
    {
        private static readonly Container<FoldingRange> s_emptyFoldingRangeContainer = new();
        private readonly ILogger _logger;
        private readonly ConfigurationService _configurationService;
        private readonly WorkspaceService _workspaceService;

        public PsesFoldingRangeHandler(ILoggerFactory factory, ConfigurationService configurationService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesFoldingRangeHandler>();
            _configurationService = configurationService;
            _workspaceService = workspaceService;
        }

        protected override FoldingRangeRegistrationOptions CreateRegistrationOptions(FoldingRangeCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        public override Task<Container<FoldingRange>> Handle(FoldingRangeRequestParam request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("FoldingRange request canceled for file: {Uri}", request.TextDocument.Uri);
                return Task.FromResult(s_emptyFoldingRangeContainer);
            }

            // TODO: Should be using dynamic registrations
            if (!_configurationService.CurrentSettings.CodeFolding.Enable)
            {
                return Task.FromResult(s_emptyFoldingRangeContainer);
            }

            // Avoid crash when using untitled: scheme or any other scheme where the document doesn't
            // have a backing file.  https://github.com/PowerShell/vscode-powershell/issues/1676
            // Perhaps a better option would be to parse the contents of the document as a string
            // as opposed to reading a file but the scenario of "no backing file" probably doesn't
            // warrant the extra effort.
            if (!_workspaceService.TryGetFile(request.TextDocument.Uri, out ScriptFile scriptFile))
            {
                return Task.FromResult(s_emptyFoldingRangeContainer);
            }

            // If we're showing the last line, decrement the Endline of all regions by one.
            int endLineOffset = _configurationService.CurrentSettings.CodeFolding.ShowLastLine ? -1 : 0;
            List<FoldingRange> folds = new();
            foreach (FoldingReference fold in TokenOperations.FoldableReferences(scriptFile.ScriptTokens).References)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                folds.Add(new FoldingRange
                {
                    EndCharacter = fold.EndCharacter,
                    EndLine = fold.EndLine + endLineOffset,
                    Kind = fold.Kind,
                    StartCharacter = fold.StartCharacter,
                    StartLine = fold.StartLine
                });
            }

            return folds.Count == 0
                ? Task.FromResult(s_emptyFoldingRangeContainer)
                : Task.FromResult(new Container<FoldingRange>(folds));
        }
    }
}
