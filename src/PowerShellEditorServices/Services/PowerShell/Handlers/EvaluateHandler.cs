﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
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
            IReadOnlyList<PSObject> results = await _executionService.ExecutePSCommandAsync<PSObject>(
                new PSCommand().AddScript(request.Expression),
                cancellationToken,
                new PowerShellExecutionOptions
                {
                    RequiresForeground = true,
                    WriteInputToHost = true,
                    WriteOutputToHost = true,
                    AddToHistory = true,
                    ThrowOnError = false,
                }).ConfigureAwait(false);

            return new EvaluateResponseBody
            {
                Result = string.Join(System.Environment.NewLine, results),
                VariablesReference = 0
            };
        }
    }
}
