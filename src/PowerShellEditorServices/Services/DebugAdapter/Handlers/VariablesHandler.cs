// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class VariablesHandler : IVariablesHandler
    {
        private readonly DebugService _debugService;

        public VariablesHandler(DebugService debugService) => _debugService = debugService;

        public Task<VariablesResponse> Handle(VariablesArguments request, CancellationToken cancellationToken)
        {
            VariableDetailsBase[] variables =
                _debugService.GetVariables(
                    (int)request.VariablesReference);

            VariablesResponse variablesResponse = null;

            try
            {
                variablesResponse = new VariablesResponse
                {
                    Variables =
                        variables
                            .Select(LspDebugUtils.CreateVariable)
                            .ToArray()
                };
            }
            catch (Exception)
            {
                // TODO: This shouldn't be so broad
            }

            return Task.FromResult(variablesResponse);
        }
    }
}
