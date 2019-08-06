using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc.Client;
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

        private readonly ILanguageServer _languageServer;

        private CodeActionCapability _capability;

        public CodeActionHandler(ILoggerFactory factory, ILanguageServer languageServer, AnalysisService analysisService)
        {
            _logger = factory.CreateLogger<TextDocumentHandler>();
            _analysisService = analysisService;
            _languageServer = languageServer;
            _registrationOptions = new CodeActionRegistrationOptions()
            {
                DocumentSelector = new DocumentSelector(new DocumentFilter() { Pattern = "**/*.ps*1" }),
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
            IReadOnlyDictionary<string, MarkerCorrection> corrections = await _analysisService.GetMostRecentCodeActionsForFileAsync(request.TextDocument.Uri.ToString());

            if (corrections == null)
            {
                // TODO: Find out if we can cache this empty value
                return new CommandOrCodeActionContainer();
            }

            var codeActions = new List<CommandOrCodeAction>();

            // If there are any code fixes, send these commands first so they appear at top of "Code Fix" menu in the client UI.
            foreach (Diagnostic diagnostic in request.Context.Diagnostics)
            {
                if (diagnostic.Code.IsLong)
                {
                    _logger.LogWarning(
                        $"textDocument/codeAction skipping diagnostic with non-string code {diagnostic.Code.Long}: {diagnostic.Source} {diagnostic.Message}");
                }
                else if (string.IsNullOrEmpty(diagnostic.Code.String))
                {
                    _logger.LogWarning(
                        $"textDocument/codeAction skipping diagnostic with empty Code field: {diagnostic.Source} {diagnostic.Message}");

                    continue;
                }


                string diagnosticId = AnalysisService.GetUniqueIdFromDiagnostic(diagnostic);
                if (corrections.TryGetValue(diagnosticId, out MarkerCorrection correction))
                {
                    codeActions.Add(new Command()
                    {
                        Title = correction.Name,
                        Name = "PowerShell.ApplyCodeActionEdits",
                        Arguments = JArray.FromObject(correction.Edits)
                    });
                }
            }

            // Add "show documentation" commands last so they appear at the bottom of the client UI.
            // These commands do not require code fixes. Sometimes we get a batch of diagnostics
            // to create commands for. No need to create multiple show doc commands for the same rule.
            var ruleNamesProcessed = new HashSet<string>();
            foreach (Diagnostic diagnostic in request.Context.Diagnostics)
            {
                if (!diagnostic.Code.IsString || string.IsNullOrEmpty(diagnostic.Code.String)) { continue; }

                if (string.Equals(diagnostic.Source, "PSScriptAnalyzer", StringComparison.OrdinalIgnoreCase) &&
                    !ruleNamesProcessed.Contains(diagnostic.Code.String))
                {
                    ruleNamesProcessed.Add(diagnostic.Code.String);

                    codeActions.Add(
                        new Command
                        {
                            Title = $"Show documentation for \"{diagnostic.Code}\"",
                            Name = "PowerShell.ShowCodeActionDocumentation",
                            Arguments = JArray.FromObject(new[] { diagnostic.Code })
                        });
                }
            }

            return codeActions;
        }
    }
}
