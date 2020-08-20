//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.TextDocument;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class GetCommentHelpHandler : IGetCommentHelpHandler
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

            int triggerLine = request.TriggerPosition.Line + 1;

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

            string helpText = await _analysisService.GetCommentHelpText(funcText, helpLocation, forBlockComment: request.BlockComment).ConfigureAwait(false);

            if (helpText == null)
            {
                return result;
            }

            List<string> helpLines = ScriptFile.GetLinesInternal(helpText);

            if (helpLocation != null &&
                !helpLocation.Equals("before", StringComparison.OrdinalIgnoreCase))
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
