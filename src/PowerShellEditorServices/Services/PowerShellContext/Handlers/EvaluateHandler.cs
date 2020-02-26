//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class EvaluateHandler : IEvaluateHandler
    {
        private readonly ILogger _logger;
        private readonly PowerShellContextService _powerShellContextService;

        public EvaluateHandler(ILoggerFactory factory, PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<EvaluateHandler>();
            _powerShellContextService = powerShellContextService;
        }

        public Task<EvaluateResponseBody> Handle(EvaluateRequestArguments request, CancellationToken cancellationToken)
        {
            _powerShellContextService.ExecuteScriptStringAsync(
                request.Expression,
                writeInputToHost: true,
                writeOutputToHost: true,
                addToHistory: true);

            return Task.FromResult(new EvaluateResponseBody
            {
                Result = "",
                VariablesReference = 0
            });
        }
    }
}
