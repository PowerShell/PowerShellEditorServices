//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.PowerShell.EditorServices.Services;
using Microsoft.PowerShell.EditorServices.Services.DebugAdapter;
using Microsoft.PowerShell.EditorServices.Utility;
using OmniSharp.Extensions.DebugAdapter.Protocol.Models;
using OmniSharp.Extensions.DebugAdapter.Protocol.Requests;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    internal class VariablesHandler : IVariablesHandler
    {
        private readonly ILogger _logger;
        private readonly DebugService _debugService;

        public VariablesHandler(
            ILoggerFactory loggerFactory,
            DebugService debugService)
        {
            _logger = loggerFactory.CreateLogger<VariablesHandler>();
            _debugService = debugService;
        }

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
