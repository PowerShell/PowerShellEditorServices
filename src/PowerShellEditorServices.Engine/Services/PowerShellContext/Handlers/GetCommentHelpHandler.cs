using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;
using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    public class GetCommentHelpHandler : IGetCommentHelpHandler
    {
        private readonly ILogger _logger;
        private readonly WorkspaceService _workspaceService;
        private readonly AnalysisService _analysisService;
        private readonly SymbolsService _symbolsService;

        public GetCommentHelpHandler(
            ILoggerFactory factory,
            WorkspaceService workspaceService,
            AnalysisService analysisService,
            SymbolsService symbolsService)
        {
            _logger = factory.CreateLogger<GetCommentHelpHandler>();
            _workspaceService = workspaceService;
            _analysisService = analysisService;
            _symbolsService = symbolsService;
        }

        public async Task<CommentHelpRequestResult> Handle(CommentHelpRequestParams request, CancellationToken cancellationToken)
        {
            var result = new CommentHelpRequestResult();

            if (!_workspaceService.TryGetFile(request.DocumentUri, out ScriptFile scriptFile))
            {
                return result;
            }

            int triggerLine = (int) request.TriggerPosition.Line + 1;

            FunctionDefinitionAst functionDefinitionAst = _symbolsService.GetFunctionDefinitionForHelpComment(
                scriptFile,
                triggerLine,
                out string helpLocation);

            if (functionDefinitionAst == null)
            {
                return result;
            }

            IScriptExtent funcExtent = functionDefinitionAst.Extent;
            string funcText = funcExtent.Text;
            if (helpLocation.Equals("begin"))
            {
                // check if the previous character is `<` because it invalidates
                // the param block the follows it.
                IList<string> lines = ScriptFile.GetLinesInternal(funcText);
                int relativeTriggerLine0b = triggerLine - funcExtent.StartLineNumber;
                if (relativeTriggerLine0b > 0 && lines[relativeTriggerLine0b].IndexOf("<", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    lines[relativeTriggerLine0b] = string.Empty;
                }

                funcText = string.Join("\n", lines);
            }

            List<ScriptFileMarker> analysisResults = await _analysisService.GetSemanticMarkersAsync(
                funcText,
                AnalysisService.GetCommentHelpRuleSettings(
                    enable: true,
                    exportedOnly: false,
                    blockComment: request.BlockComment,
                    vscodeSnippetCorrection: true,
                    placement: helpLocation));

            string helpText = analysisResults?.FirstOrDefault()?.Correction?.Edits[0].Text;

            if (helpText == null)
            {
                return result;
            }

            result.Content = ScriptFile.GetLinesInternal(helpText).ToArray();

            if (helpLocation != null &&
                !helpLocation.Equals("before", StringComparison.OrdinalIgnoreCase))
            {
                // we need to trim the leading `{` and newline when helpLocation=="begin"
                // we also need to trim the leading newline when helpLocation=="end"
                result.Content = result.Content.Skip(1).ToArray();
            }

            return result;
        }
    }
}
