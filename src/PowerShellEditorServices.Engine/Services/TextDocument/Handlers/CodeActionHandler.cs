using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using PowerShellEditorServices.Engine.Services.Handlers;

namespace Microsoft.PowerShell.EditorServices.TextDocument
{
    internal class CodeActionHandler : ICodeActionHandler
    {
        private static readonly CodeActionKind[] s_supportedCodeActions = new[]
        {
            CodeActionKind.QuickFix
        };

        private readonly CodeActionRegistrationOptions _registrationOptions;

        private readonly ILogger _logger;

        private readonly AnalysisService _analysisService;

        private readonly WorkspaceService _workspaceService;

        public CodeActionHandler(ILoggerFactory factory, AnalysisService analysisService, WorkspaceService workspaceService)
        {
            _logger = factory.CreateLogger<TextDocumentHandler>();
            _analysisService = analysisService;
            _workspaceService = workspaceService;
            _registrationOptions = new CodeActionRegistrationOptions()
            {
                DocumentSelector = new DocumentSelector(new DocumentFilter() { Pattern = "**/*.ps*1" }),
                CodeActionKinds = s_supportedCodeActions
            };
        }

        public CodeActionRegistrationOptions GetRegistrationOptions()
        {
            throw new System.NotImplementedException();
        }

        public Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            MarkerCorrection correction = null;
            Dictionary<string, MarkerCorrection> markerIndex = null;
            var codeActionCommands = new List<CodeActionCommand>();

            // If there are any code fixes, send these commands first so they appear at top of "Code Fix" menu in the client UI.
            if (this.codeActionsPerFile.TryGetValue(codeActionParams.TextDocument.Uri, out markerIndex))
            {
                foreach (var diagnostic in codeActionParams.Context.Diagnostics)
                {
                    if (string.IsNullOrEmpty(diagnostic.Code))
                    {
                        _logger.LogWarning(
                            $"textDocument/codeAction skipping diagnostic with empty Code field: {diagnostic.Source} {diagnostic.Message}");

                        continue;
                    }

                    string diagnosticId = GetUniqueIdFromDiagnostic(diagnostic);
                    if (markerIndex.TryGetValue(diagnosticId, out correction))
                    {
                        codeActionCommands.Add(
                            new CodeActionCommand
                            {
                                Title = correction.Name,
                                Command = "PowerShell.ApplyCodeActionEdits",
                                Arguments = JArray.FromObject(correction.Edits)
                            });
                    }
                }
            }

            // Add "show documentation" commands last so they appear at the bottom of the client UI.
            // These commands do not require code fixes. Sometimes we get a batch of diagnostics
            // to create commands for. No need to create multiple show doc commands for the same rule.
            var ruleNamesProcessed = new HashSet<string>();
            foreach (var diagnostic in codeActionParams.Context.Diagnostics)
            {
                if (string.IsNullOrEmpty(diagnostic.Code)) { continue; }

                if (string.Equals(diagnostic.Source, "PSScriptAnalyzer", StringComparison.OrdinalIgnoreCase) &&
                    !ruleNamesProcessed.Contains(diagnostic.Code))
                {
                    ruleNamesProcessed.Add(diagnostic.Code);

                    codeActionCommands.Add(
                        new CodeActionCommand
                        {
                            Title = $"Show documentation for \"{diagnostic.Code}\"",
                            Command = "PowerShell.ShowCodeActionDocumentation",
                            Arguments = JArray.FromObject(new[] { diagnostic.Code })
                        });
                }
            }

            await requestContext.SendResultAsync(
                codeActionCommands.ToArray());
        }

        public void SetCapability(CodeActionCapability capability)
        {
            throw new System.NotImplementedException();
        }
    }
}
