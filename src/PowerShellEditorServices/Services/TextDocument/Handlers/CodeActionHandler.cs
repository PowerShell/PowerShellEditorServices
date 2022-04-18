// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesCodeActionHandler : CodeActionHandlerBase
    {
        private readonly ILogger _logger;
        private readonly AnalysisService _analysisService;

        public PsesCodeActionHandler(ILoggerFactory factory, AnalysisService analysisService)
        {
            _logger = factory.CreateLogger<PsesCodeActionHandler>();
            _analysisService = analysisService;
        }

        protected override CodeActionRegistrationOptions CreateRegistrationOptions(CodeActionCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            // TODO: What do we do with the arguments?
            DocumentSelector = LspUtils.PowerShellDocumentSelector,
            CodeActionKinds = new CodeActionKind[] { CodeActionKind.QuickFix }
        };

        // TODO: Either fix or ignore "method lacks 'await'" warning.
        public override async Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken)
        {
            // TODO: How on earth do we handle a CodeAction? This is new...
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("CodeAction request canceled for: {Title}", request.Title);
            }
            return request;
        }

        public override async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug($"CodeAction request canceled at range: {request.Range}");
                return Array.Empty<CommandOrCodeAction>();
            }

            IReadOnlyDictionary<string, IEnumerable<MarkerCorrection>> corrections = await _analysisService.GetMostRecentCodeActionsForFileAsync(
                request.TextDocument.Uri)
                .ConfigureAwait(false);

            if (corrections == null)
            {
                return Array.Empty<CommandOrCodeAction>();
            }

            List<CommandOrCodeAction> codeActions = new();

            // If there are any code fixes, send these commands first so they appear at top of "Code Fix" menu in the client UI.
            foreach (Diagnostic diagnostic in request.Context.Diagnostics)
            {
                if (string.IsNullOrEmpty(diagnostic.Code?.String))
                {
                    _logger.LogWarning(
                        $"textDocument/codeAction skipping diagnostic with empty Code field: {diagnostic.Source} {diagnostic.Message}");

                    continue;
                }

                string diagnosticId = AnalysisService.GetUniqueIdFromDiagnostic(diagnostic);
                if (corrections.TryGetValue(diagnosticId, out IEnumerable<MarkerCorrection> markerCorrections))
                {
                    foreach (MarkerCorrection markerCorrection in markerCorrections)
                    {
                        codeActions.Add(new CodeAction
                        {
                            Title = markerCorrection.Name,
                            Kind = CodeActionKind.QuickFix,
                            Edit = new WorkspaceEdit
                            {
                                DocumentChanges = new Container<WorkspaceEditDocumentChange>(
                                    new WorkspaceEditDocumentChange(
                                        new TextDocumentEdit
                                        {
                                            TextDocument = new OptionalVersionedTextDocumentIdentifier
                                            {
                                                Uri = request.TextDocument.Uri
                                            },
                                            Edits = new TextEditContainer(ScriptRegion.ToTextEdit(markerCorrection.Edit))
                                        }))
                            }
                        });
                    }
                }
            }

            // Add "show documentation" commands last so they appear at the bottom of the client UI.
            // These commands do not require code fixes. Sometimes we get a batch of diagnostics
            // to create commands for. No need to create multiple show doc commands for the same rule.
            HashSet<string> ruleNamesProcessed = new();
            foreach (Diagnostic diagnostic in request.Context.Diagnostics)
            {
                if (
                    !diagnostic.Code.HasValue ||
                    !diagnostic.Code.Value.IsString ||
                    string.IsNullOrEmpty(diagnostic.Code?.String))
                {
                    continue;
                }

                if (string.Equals(diagnostic.Source, "PSScriptAnalyzer", StringComparison.OrdinalIgnoreCase) &&
                    !ruleNamesProcessed.Contains(diagnostic.Code?.String))
                {
                    _ = ruleNamesProcessed.Add(diagnostic.Code?.String);
                    string title = $"Show documentation for: {diagnostic.Code?.String}";
                    codeActions.Add(new CodeAction
                    {
                        Title = title,
                        // This doesn't fix anything, but I'm adding it here so that it shows up in VS Code's
                        // Quick fix UI. The VS Code team is working on a way to support documentation CodeAction's better
                        // but this is good for now until that's ready.
                        Kind = CodeActionKind.QuickFix,
                        Command = new Command
                        {
                            Title = title,
                            Name = "PowerShell.ShowCodeActionDocumentation",
                            Arguments = Newtonsoft.Json.Linq.JArray.FromObject(new[] { diagnostic.Code?.String })
                        }
                    });
                }
            }

            return codeActions;
        }
    }
}
