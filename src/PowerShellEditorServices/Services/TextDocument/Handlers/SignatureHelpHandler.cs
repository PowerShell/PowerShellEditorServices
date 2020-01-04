//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.Symbols;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    public class SignatureHelpHandler : ISignatureHelpHandler
    {
        private static readonly SignatureInformation[] s_emptySignatureResult = new SignatureInformation[0];

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Language = "powershell"
            }
        );

        private readonly ILogger _logger;
        private readonly SymbolsService _symbolsService;
        private readonly WorkspaceService _workspaceService;
        private readonly PowerShellContextService _powerShellContextService;

        private SignatureHelpCapability _capability;

        public SignatureHelpHandler(
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

        public SignatureHelpRegistrationOptions GetRegistrationOptions()
        {
            return new SignatureHelpRegistrationOptions
            {
                DocumentSelector = _documentSelector,
                // A sane default of " ". We may be able to include others like "-".
                TriggerCharacters = new Container<string>(" ")
            };
        }

        public async Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);

            ParameterSetSignatures parameterSets =
                await _symbolsService.FindParameterSetsInFileAsync(
                    scriptFile,
                    (int) request.Position.Line + 1,
                    (int) request.Position.Character + 1,
                    _powerShellContextService);

            SignatureInformation[] signatures = s_emptySignatureResult;

            if (parameterSets != null)
            {
                signatures = new SignatureInformation[parameterSets.Signatures.Length];
                for (int i = 0; i < signatures.Length; i++)
                {
                    var parameters = new ParameterInformation[parameterSets.Signatures[i].Parameters.Count()];
                    int j = 0;
                    foreach (ParameterInfo param in parameterSets.Signatures[i].Parameters)
                    {
                        parameters[j] = CreateParameterInfo(param);
                        j++;
                    }

                    signatures[i] = new SignatureInformation
                    {
                        Label = parameterSets.CommandName + " " + parameterSets.Signatures[i].SignatureText,
                        Documentation = null,
                        Parameters = parameters,
                    };
                }
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
