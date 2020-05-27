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
    internal class PsesSignatureHelpHandler : ISignatureHelpHandler
    {
        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;
        private readonly PowerShellContextService _powerShellContextService;

        private SignatureHelpCapability _capability;

        public PsesSignatureHelpHandler(
            ILoggerFactory factory,
            SymbolsService symbolsService,
            WorkspaceService workspaceService,
            PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<PsesHoverHandler>();
            _symbolsService = symbolsService;
            _workspaceService = workspaceService;
            _powerShellContextService = powerShellContextService;
        }

        public SignatureHelpRegistrationOptions GetRegistrationOptions()
        {
            return new SignatureHelpRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector,
                // A sane default of " ". We may be able to include others like "-".
                TriggerCharacters = new Container<string>(" ")
            };
        }

        public async Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("SignatureHelp request canceled for file: {0}", request.TextDocument.Uri);
                return new SignatureHelp();
            }

            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            ParameterSetSignatures parameterSets =
                await _symbolsService.FindParameterSetsInFileAsync(
                    scriptFile,
                    request.Position.Line + 1,
                    request.Position.Character + 1,
                    _powerShellContextService).ConfigureAwait(false);

            if (parameterSets == null)
            {
                return new SignatureHelp();
            }

            var signatures = new SignatureInformation[parameterSets.Signatures.Length];
            for (int i = 0; i < signatures.Length; i++)
            {
                var parameters = new List<ParameterInformation>();
                foreach (ParameterInfo param in parameterSets.Signatures[i].Parameters)
                {
                    parameters.Add(CreateParameterInfo(param));
                }

                signatures[i] = new SignatureInformation
                {
                    Label = parameterSets.CommandName + " " + parameterSets.Signatures[i].SignatureText,
                    Documentation = null,
                    Parameters = parameters,
                };
            }

            return new SignatureHelp
            {
                Signatures = signatures,
                ActiveParameter = null,
                ActiveSignature = 0
            };
        }

        public void SetCapability(SignatureHelpCapability capability)
        {
            _capability = capability;
        }

        private static ParameterInformation CreateParameterInfo(ParameterInfo parameterInfo)
        {
            return new ParameterInformation
            {
                Label = parameterInfo.Name,
                Documentation = string.Empty
            };
        }
    }
}
