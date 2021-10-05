// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Debugging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class DebugEvaluateHandler : IEvaluateHandler
    {
        private readonly ILogger _logger;
        private readonly IPowerShellDebugContext _debugContext;
        private readonly PowerShellExecutionService _executionService;
        private readonly DebugService _debugService;

        public DebugEvaluateHandler(
            ILoggerFactory factory,
            IPowerShellDebugContext debugContext,
            PowerShellExecutionService executionService,
            DebugService debugService)
        {
            _logger = factory.CreateLogger<DebugEvaluateHandler>();
            _debugContext = debugContext;
            _executionService = executionService;
            _debugService = debugService;
        }

        public async Task<EvaluateResponseBody> Handle(EvaluateRequestArguments request, CancellationToken cancellationToken)
        {
            string valueString = "";
            int variableId = 0;

            bool isFromRepl =
                string.Equals(
                    request.Context,
                    "repl",
                    StringComparison.CurrentCultureIgnoreCase);

            if (isFromRepl)
            {
                _executionService.ExecutePSCommandAsync(
                    new PSCommand().AddScript(request.Expression),
                    CancellationToken.None,
                    new PowerShellExecutionOptions { WriteOutputToHost = true, ThrowOnError = false, AddToHistory = true }).HandleErrorsAsync(_logger);
            }
            else
            {
                VariableDetailsBase result = null;

                // VS Code might send this request after the debugger
                // has been resumed, return an empty result in this case.
                if (_debugContext.IsStopped)
                {
                    // First check to see if the watch expression refers to a naked variable reference.
                    result =
                        _debugService.GetVariableFromExpression(request.Expression, request.FrameId);

                    // If the expression is not a naked variable reference, then evaluate the expression.
                    if (result == null)
                    {
                        result =
                            await _debugService.EvaluateExpressionAsync(
                                request.Expression,
                                request.FrameId,
                                isFromRepl).ConfigureAwait(false);
                    }
                }

                if (result != null)
                {
                    valueString = result.ValueString;
                    variableId =
                        result.IsExpandable ?
                            result.Id : 0;
                }
            }

            return new EvaluateResponseBody
            {
                Result = valueString,
                VariablesReference = variableId
            };
        }
    }
}
