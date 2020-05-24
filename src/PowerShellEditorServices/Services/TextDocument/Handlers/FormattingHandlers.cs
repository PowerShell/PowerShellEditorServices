//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    // TODO: Add IDocumentOnTypeFormatHandler to support on-type formatting.
    internal class PsesDocumentFormattingHandlers : IDocumentFormattingHandler, IDocumentRangeFormattingHandler
    {
        private readonly ILogger _logger;
        private readonly AnalysisService _analysisService;
        private readonly ConfigurationService _configurationService;
        private readonly WorkspaceService _workspaceService;

        private DocumentFormattingCapability _documentFormattingCapability;
        private DocumentRangeFormattingCapability _documentRangeFormattingCapability;

        public PsesDocumentFormattingHandlers(
            ILoggerFactory factory,
            AnalysisService analysisService,
            ConfigurationService configurationService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesDocumentFormattingHandlers>();
            _analysisService = analysisService;
            _configurationService = configurationService;
            _workspaceService = workspaceService;
        }

        public DocumentFormattingRegistrationOptions GetRegistrationOptions()
        {
            return new DocumentFormattingRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector
            };
        }

        DocumentRangeFormattingRegistrationOptions IRegistration<DocumentRangeFormattingRegistrationOptions>.GetRegistrationOptions()
        {
            return new DocumentRangeFormattingRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector
            };
        }

        public async Task<TextEditContainer> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
        {
            var scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            var pssaSettings = _configurationService.CurrentSettings.CodeFormatting.GetPSSASettingsHashtable(
                (int)request.Options.TabSize,
                request.Options.InsertSpaces,
                _logger);


            // TODO raise an error event in case format returns null
            string formattedScript;
            Range editRange;
            var extent = scriptFile.ScriptAst.Extent;

            // todo create an extension for converting range to script extent
            editRange = new Range
            {
                Start = new Position
                {
                    Line = extent.StartLineNumber - 1,
                    Character = extent.StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = extent.EndLineNumber - 1,
                    Character = extent.EndColumnNumber - 1
                }
            };

            formattedScript = await _analysisService.FormatAsync(
                scriptFile.Contents,
                pssaSettings,
                null).ConfigureAwait(false);
            formattedScript = formattedScript ?? scriptFile.Contents;

            return new TextEditContainer(new TextEdit
            {
                NewText = formattedScript,
                Range = editRange
            });
        }

        public async Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken cancellationToken)
        {
            var scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            var pssaSettings = _configurationService.CurrentSettings.CodeFormatting.GetPSSASettingsHashtable(
                (int)request.Options.TabSize,
                request.Options.InsertSpaces,
                _logger);

            // TODO raise an error event in case format returns null;
            string formattedScript;
            Range editRange;
            var extent = scriptFile.ScriptAst.Extent;

            // TODO create an extension for converting range to script extent
            editRange = new Range
            {
                Start = new Position
                {
                    Line = extent.StartLineNumber - 1,
                    Character = extent.StartColumnNumber - 1
                },
                End = new Position
                {
                    Line = extent.EndLineNumber - 1,
                    Character = extent.EndColumnNumber - 1
                }
            };

            Range range = request.Range;
            var rangeList = range == null ? null : new int[]
            {
                range.Start.Line + 1,
                range.Start.Character + 1,
                range.End.Line + 1,
                range.End.Character + 1
            };

            formattedScript = await _analysisService.FormatAsync(
                scriptFile.Contents,
                pssaSettings,
                rangeList).ConfigureAwait(false);

            if (formattedScript == null)
            {
                _logger.LogWarning("Formatting returned null. Returning original contents for file: {0}", scriptFile.DocumentUri);
                formattedScript = scriptFile.Contents;
            }

            return new TextEditContainer(new TextEdit
            {
                NewText = formattedScript,
                Range = editRange
            });
        }

        public void SetCapability(DocumentFormattingCapability capability)
        {
            _documentFormattingCapability = capability;
        }

        public void SetCapability(DocumentRangeFormattingCapability capability)
        {
            _documentRangeFormattingCapability = capability;
        }
    }
}
