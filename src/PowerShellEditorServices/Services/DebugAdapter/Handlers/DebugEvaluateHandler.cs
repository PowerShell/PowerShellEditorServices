//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class DebugEvaluateHandler : IEvaluateHandler
    {
        private readonly ILogger _logger;
        private readonly PowerShellContextService _powerShellContextService;
        private readonly DebugService _debugService;

        public DebugEvaluateHandler(
            ILoggerFactory factory,
            PowerShellContextService powerShellContextService,
            DebugService debugService)
        {
            _logger = factory.CreateLogger<DebugEvaluateHandler>();
            _powerShellContextService = powerShellContextService;
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
                var notAwaited =
                    _powerShellContextService
                        .ExecuteScriptStringAsync(request.Expression, false, true)
                        .ConfigureAwait(false);
            }
            else
            {
                VariableDetailsBase result = null;

                // VS Code might send this request after the debugger
                // has been resumed, return an empty result in this case.
                if (_powerShellContextService.IsDebuggerStopped)
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
