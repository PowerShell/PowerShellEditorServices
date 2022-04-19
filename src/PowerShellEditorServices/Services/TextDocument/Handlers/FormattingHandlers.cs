// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    // TODO: Add IDocumentOnTypeFormatHandler to support on-type formatting.
    internal class PsesDocumentFormattingHandler : DocumentFormattingHandlerBase
    {
        private readonly ILogger _logger;
        private readonly AnalysisService _analysisService;
        private readonly ConfigurationService _configurationService;
        private readonly WorkspaceService _workspaceService;

        public PsesDocumentFormattingHandler(
            ILoggerFactory factory,
            AnalysisService analysisService,
            ConfigurationService configurationService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesDocumentFormattingHandler>();
            _analysisService = analysisService;
            _configurationService = configurationService;
            _workspaceService = workspaceService;
        }

        protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(DocumentFormattingCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        public override async Task<TextEditContainer> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
        {
            Services.TextDocument.ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            System.Collections.Hashtable pssaSettings = _configurationService.CurrentSettings.CodeFormatting.GetPSSASettingsHashtable(
                request.Options.TabSize,
                request.Options.InsertSpaces,
                _logger);

            // TODO: Raise an error event in case format returns null.
            string formattedScript;
            Range editRange;
            System.Management.Automation.Language.IScriptExtent extent = scriptFile.ScriptAst.Extent;

            // TODO: Create an extension for converting range to script extent.
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

            if (formattedScript is null)
            {
                _logger.LogWarning($"Formatting returned null. Not formatting: {scriptFile.DocumentUri}");
                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning($"Formatting request canceled for: {scriptFile.DocumentUri}");
                return null;
            }

            return new TextEditContainer(new TextEdit
            {
                NewText = formattedScript,
                Range = editRange
            });
        }
    }

    internal class PsesDocumentRangeFormattingHandler : DocumentRangeFormattingHandlerBase
    {
        private readonly ILogger _logger;
        private readonly AnalysisService _analysisService;
        private readonly ConfigurationService _configurationService;
        private readonly WorkspaceService _workspaceService;

        public PsesDocumentRangeFormattingHandler(
            ILoggerFactory factory,
            AnalysisService analysisService,
            ConfigurationService configurationService,
            WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesDocumentRangeFormattingHandler>();
            _analysisService = analysisService;
            _configurationService = configurationService;
            _workspaceService = workspaceService;
        }

        protected override DocumentRangeFormattingRegistrationOptions CreateRegistrationOptions(DocumentRangeFormattingCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = LspUtils.PowerShellDocumentSelector
        };

        public override async Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken cancellationToken)
        {
            Services.TextDocument.ScriptFile scriptFile = _workspaceService.GetFile(request.TextDocument.Uri);
            System.Collections.Hashtable pssaSettings = _configurationService.CurrentSettings.CodeFormatting.GetPSSASettingsHashtable(
                request.Options.TabSize,
                request.Options.InsertSpaces,
                _logger);

            // TODO: Raise an error event in case format returns null.
            string formattedScript;
            Range editRange;
            System.Management.Automation.Language.IScriptExtent extent = scriptFile.ScriptAst.Extent;

            // TODO: Create an extension for converting range to script extent.
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
            int[] rangeList = range == null ? null : new int[]
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

            if (formattedScript is null)
            {
                _logger.LogWarning($"Formatting returned null. Not formatting: {scriptFile.DocumentUri}");
                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning($"Formatting request canceled for: {scriptFile.DocumentUri}");
                return null;
            }

            return new TextEditContainer(new TextEdit
            {
                NewText = formattedScript,
                Range = editRange
            });
        }
    }
}
