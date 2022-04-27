// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IInternalPowerShellExecutionService _executionService;

        public EvaluateHandler(IInternalPowerShellExecutionService executionService) => _executionService = executionService;

        public async Task<EvaluateResponseBody> Handle(EvaluateRequestArguments request, CancellationToken cancellationToken)
        {
            // This API is mostly used for F8 execution so it requires the foreground.
            await _executionService.ExecutePSCommandAsync(
                new PSCommand().AddScript(request.Expression),
                CancellationToken.None,
                new PowerShellExecutionOptions
                {
                    RequiresForeground = true,
                    WriteInputToHost = true,
                    WriteOutputToHost = true,
                    AddToHistory = true,
                    ThrowOnError = false,
                }).ConfigureAwait(false);

            // TODO: Should we return a more informative result?
            return new EvaluateResponseBody
            {
                Result = "",
                VariablesReference = 0
            };
        }
    }
}
