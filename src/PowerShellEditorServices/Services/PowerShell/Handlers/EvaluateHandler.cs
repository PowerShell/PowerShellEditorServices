// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services.PowerShell;
using Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    /// <summary>
    /// Handler for a custom request type for evaluating PowerShell.
    /// This is generally for F8 support, to allow execution of a highlighted code snippet in the console as if it were copy-pasted.
    /// </summary>
    internal class EvaluateHandler : IEvaluateHandler
    {
        private readonly ILogger _logger;
        private readonly IInternalPowerShellExecutionService _executionService;

        public EvaluateHandler(
            ILoggerFactory factory,
            IInternalPowerShellExecutionService executionService)
        {
            _logger = factory.CreateLogger<EvaluateHandler>();
            _executionService = executionService;
        }

        public Task<EvaluateResponseBody> Handle(EvaluateRequestArguments request, CancellationToken cancellationToken)
        {
            // TODO: Understand why we currently handle this asynchronously and why we return a dummy result value
            //       instead of awaiting the execution and returing a real result of some kind

            // This API is mostly used for F8 execution, so needs to interrupt the command prompt
            _executionService.ExecutePSCommandAsync(
                new PSCommand().AddScript(request.Expression),
                CancellationToken.None,
                new PowerShellExecutionOptions { WriteInputToHost = true, WriteOutputToHost = true, AddToHistory = true, ThrowOnError = false, InterruptCurrentForeground = true });

            return Task.FromResult(new EvaluateResponseBody
            {
                Result = "",
                VariablesReference = 0
            });
        }
    }
}
