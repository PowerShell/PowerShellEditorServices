// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class GetCommentHelpHandler : IGetCommentHelpHandler
    {
        private readonly WorkspaceService _workspaceService;
        private readonly AnalysisService _analysisService;

        public GetCommentHelpHandler(
            WorkspaceService workspaceService,
            AnalysisService analysisService)
        {
            _workspaceService = workspaceService;
            _analysisService = analysisService;
        }

        public async Task<CommentHelpRequestResult> Handle(CommentHelpRequestParams request, CancellationToken cancellationToken)
        {
            CommentHelpRequestResult result = new();

            if (!_workspaceService.TryGetFile(request.DocumentUri, out ScriptFile scriptFile))
            {
                return result;
            }

            int triggerLine = request.TriggerPosition.Line + 1;

            FunctionDefinitionAst functionDefinitionAst = SymbolsService.GetFunctionDefinitionForHelpComment(
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

            string helpText = await _analysisService.GetCommentHelpText(funcText, helpLocation, forBlockComment: request.BlockComment).ConfigureAwait(false);

            if (helpText == null)
            {
                return result;
            }

            List<string> helpLines = ScriptFile.GetLinesInternal(helpText);

            if (helpLocation?.Equals("before", StringComparison.OrdinalIgnoreCase) == false)
            {
                // we need to trim the leading `{` and newline when helpLocation=="begin"
                helpLines.RemoveAt(helpLines.Count - 1);

                // we also need to trim the leading newline when helpLocation=="end"
                helpLines.RemoveAt(0);
            }

            // Trim trailing newline from help text.
            if (string.IsNullOrEmpty(helpLines[helpLines.Count - 1]))
            {
                helpLines.RemoveAt(helpLines.Count - 1);
            }

            result.Content = helpLines.ToArray();
            return result;
        }
    }
}
