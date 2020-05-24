//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesFoldingRangeHandler : IFoldingRangeHandler
    {
        private readonly ILogger _logger;
        private readonly ConfigurationService _configurationService;
        private readonly WorkspaceService _workspaceService;

        private FoldingRangeCapability _capability;

        public PsesFoldingRangeHandler(ILoggerFactory factory, ConfigurationService configurationService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<FoldingRangeHandler>();
            _configurationService = configurationService;
            _workspaceService = workspaceService;
        }

        public FoldingRangeRegistrationOptions GetRegistrationOptions()
        {
            return new FoldingRangeRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector
            };
        }

        public Task<Container<FoldingRange>> Handle(FoldingRangeRequestParam request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("FoldingRange request canceled for file: {0}", request.TextDocument.Uri);
                return Task.FromResult(new Container<FoldingRange>());
            }

            // TODO Should be using dynamic registrations
            if (!_configurationService.CurrentSettings.CodeFolding.Enable) { return null; }

            // Avoid crash when using untitled: scheme or any other scheme where the document doesn't
            // have a backing file.  https://github.com/PowerShell/vscode-powershell/issues/1676
            // Perhaps a better option would be to parse the contents of the document as a string
            // as opposed to reading a file but the scenario of "no backing file" probably doesn't
            // warrant the extra effort.
            if (!_workspaceService.TryGetFile(request.TextDocument.Uri, out ScriptFile scriptFile)) { return null; }

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
