using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices;

namespace PowerShellEditorServices.Engine.Services.Handlers
{
    public class EvaluateHandler : IEvaluateHandler
    {
        private readonly ILogger _logger;
        private readonly PowerShellContextService _powerShellContextService;

        public EvaluateHandler(ILoggerFactory factory, PowerShellContextService powerShellContextService)
        {
            _logger = factory.CreateLogger<EvaluateHandler>();
            _powerShellContextService = powerShellContextService;
        }

        public async Task<EvaluateResponseBody> Handle(EvaluateRequestArguments request, CancellationToken cancellationToken)
        {
            await _powerShellContextService.ExecuteScriptStringAsync(
                request.Expression,
                writeInputToHost: true,
                writeOutputToHost: true,
                addToHistory: true);

            return new EvaluateResponseBody
            {
                Result = "",
                VariablesReference = 0
            };
        }
    }
}
