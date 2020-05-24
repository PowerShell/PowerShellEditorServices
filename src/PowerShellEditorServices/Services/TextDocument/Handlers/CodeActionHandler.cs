//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;
using Microsoft.PowerShell.EditorServices.Utility;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class PsesCodeActionHandler : ICodeActionHandler
    {
        private static readonly CodeActionKind[] s_supportedCodeActions = new[]
        {
            CodeActionKind.QuickFix
        };

        private readonly CodeActionRegistrationOptions _registrationOptions;

        private readonly ILogger _logger;

        private readonly AnalysisService _analysisService;

        private readonly WorkspaceService _workspaceService;

        private CodeActionCapability _capability;

        public PsesCodeActionHandler(ILoggerFactory factory, AnalysisService analysisService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<PsesCodeActionHandler>();
            _analysisService = analysisService;
            _workspaceService = workspaceService;
            _registrationOptions = new CodeActionRegistrationOptions
            {
                DocumentSelector = LspUtils.PowerShellDocumentSelector,
                CodeActionKinds = s_supportedCodeActions
            };
        }

        public CodeActionRegistrationOptions GetRegistrationOptions()
        {
            return _registrationOptions;
        }

        public void SetCapability(CodeActionCapability capability)
        {
            _capability = capability;
        }

        public async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("CodeAction request canceled at range: {0}", request.Range);
                return Array.Empty<CommandOrCodeAction>();
            }

            // On Windows, VSCode still gives us file URIs like "file:///c%3a/...", so we need to escape them
            IReadOnlyDictionary<string, MarkerCorrection> corrections = await _analysisService.GetMostRecentCodeActionsForFileAsync(
                _workspaceService.GetFile(request.TextDocument.Uri)).ConfigureAwait(false);

            if (corrections == null)
            {
                return Array.Empty<CommandOrCodeAction>();
            }

            var codeActions = new List<CommandOrCodeAction>();

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
                if (corrections.TryGetValue(diagnosticId, out MarkerCorrection correction))
                {
                    codeActions.Add(new CodeAction
                    {
                        Title = correction.Name,
                        Kind = CodeActionKind.QuickFix,
                        Edit = new WorkspaceEdit
                        {
                            DocumentChanges = new Container<WorkspaceEditDocumentChange>(
                                new WorkspaceEditDocumentChange(
                                    new TextDocumentEdit
                                    {
                                        TextDocument = new VersionedTextDocumentIdentifier
                                        {
                                            Uri = request.TextDocument.Uri
                                        },
                                        Edits = new TextEditContainer(correction.Edits.Select(ScriptRegion.ToTextEdit))
                                    }))
                        }
                    });
                }
            }

            // Add "show documentation" commands last so they appear at the bottom of the client UI.
            // These commands do not require code fixes. Sometimes we get a batch of diagnostics
            // to create commands for. No need to create multiple show doc commands for the same rule.
            var ruleNamesProcessed = new HashSet<string>();
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
                    ruleNamesProcessed.Add(diagnostic.Code?.String);
                    var title = $"Show documentation for: {diagnostic.Code?.String}";
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
                            Arguments = JArray.FromObject(new[] { diagnostic.Code?.String })
                        }
                    });
                }
            }

            return codeActions;
        }
    }
}
